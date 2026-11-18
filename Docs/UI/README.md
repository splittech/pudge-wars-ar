# UI Toolkit: MVVM

ТДД v1.39. Все пять runtime-интерфейсов используют заранее настроенные UIDocument и типизированные ScriptableObject ViewModel.

## Зависимости и поток данных

```text
View → ViewModel → Model

пользователь → View → метод ViewModel → метод сервиса
событие / состояние сервиса → ViewModel → R3-свойство → View
```

Model — не UI-asset: это игровая симуляция, сетевые адаптеры и сервисы приложения. Model не зависит от ViewModel или документа. Например, ArenaPlacementFlow реализует IArenaPlacementService и публикует ArenaPlacementStatus; ему неизвестны кнопки, UiViewModel, UIDocument и их жизненный цикл.

ViewModel — единственная точка взаимодействия View с приложением. Она подписывается на Model, форматирует данные и предоставляет типизированные методы намерений. Имена элементов UXML используются только в View. Общей шины строковых UI-событий нет.

View — UIDocument с UXML/USS и небольшим компонентом DocumentView. Компонент связывает элементы с типизированными R3-свойствами и вызывает методы ViewModel. Он не отправляет сетевые команды и не ищет HeroNet/MatchStateNet. Исключение по работе с Unity-миром — экранная проекция готовых мировых позиций HP-баров в MatchDocument.

## Файлы и размещение

| Экран | View | ViewModel | Источник Model |
|---|---|---|---|
| Главное меню | MenuDocument | MenuViewModel | NetworkSessionController |
| Лобби / HUD / результаты / тренировка | MatchDocument | MatchViewModel | HeroNet, MatchStateNet, callbacks тренировки и навигации |
| Установка арены / потеря трекинга | PlacementDocument | PlacementViewModel | IArenaPlacementService |
| Диагностика | OverlayDocument | OverlayViewModel | IDiagnosticsSource |
| Загрузка | LoadingDocument | LoadingViewModel | ILoadingStateSource |

- Компоненты View и конкретные ViewModel: `Assets/Game/Scripts/UI/`.
- Общая инфраструктура: `Assets/Game/Scripts/UI/Foundation/`.
- Настроенные префабы: `Assets/Game/Prefabs/UI/<Screen> Document.prefab`.
- Шаблоны ViewModel: `Assets/Game/UI/ViewModels/<Screen> ViewModel.asset`.
- Разметка, стили и PanelSettings: `Assets/Game/Resources/UI/`.

Папка Resources сохранена для существующих визуальных assets, но runtime-экраны больше не используют Resources.Load. Ссылки назначены заранее.

В Main Menu Scene находится MenuDocument из Network Menu.prefab. В Gameplay Scene документы Match и Placement являются дочерними объектами GameplayScope, вне ArenaRoot. В Boot Scene Loading находится под сервисом загрузки, Overlay — под DevelopmentOverlay. Boot сохраняет их между переходами.

| Документ | sortingOrder |
|---|---|
| Menu / Match | 0 |
| Placement | 50 |
| Overlay | 100 |
| Loading | 1000 |

Все используют общий PanelSettings с масштабированием Scale With Screen Size, reference resolution 1920×1080, Match 0.5. Каждый UIDocument имеет собственный sortingOrder; настройки панели не клонируются.

## Ответственность ViewModel

**MenuViewModel** хранит Nickname, Address, Message, список серверов, доступность запуска и признаки отмены/обрыва. Методы CreateGame, Join, Search, Cancel, JoinServer и другие вызывают NetworkSessionController. Навигация и переключение AR/Flat передаются App через callbacks.

**MatchViewModel** хранит признаки видимости лобби/HUD/результатов/тренировки, счёт, таймер, здоровье, золото, перезарядку, kill feed, команды, статистику, подписи/доступность четырёх улучшений. Подписывается на HeroNet и MatchStateNet через R3; при замене локального героя/матча снимает предыдущие подписки. Методы выбора команды, готовности, старта, покупки и реванша вызывают существующие MatchActions. Проверки на сервере сохранены.

**PlacementViewModel** адаптирует ArenaPlacementStatus в Message, PanelVisible, ConfirmVisible, CanConfirm, FallbackVisible и TrackingLost. Confirm вызывает сервис только при CanConfirm; UseFlat вызывает его метод смены режима. Потеря трекинга и блокировка игрового ввода остаются обязанностью ArenaPlacementFlow.

**OverlayViewModel** получает DiagnosticsSnapshot: текст FPS/RTT/prediction и текущий режим. ToggleArenaMode обращается к источнику диагностики. Сам сбор метрик остаётся в DevelopmentOverlay, выключаемом в обычной сборке.

**LoadingViewModel** получает LoadingSnapshot: описание, прогресс, видимость и прозрачность. SimpleLoadingScreen публикует эти данные, сохраняя отмену, минимальное время показа, сглаживание прогресса и переключения GameLoadingScreen. LoadingDocument управляет USS-классом transparent; визуальное затухание остаётся в USS.

## Жизненный цикл и DI

1. В префабе DocumentView хранится ссылка Template на asset конкретной ViewModel. Поле Runtime Model в сохранённом префабе пустое.
2. При первом обращении DocumentView создаёт независимый Instantiate-клон шаблона. Assets и несколько экземпляров экранов не разделяют изменяемое состояние.
3. MainMenuScope, GameplayScope и ProjectScope получают документы через сериализованные ссылки и вызывают Initialize у их ViewModel.
4. View подписывается на свойства R3. Они немедленно передают текущее значение. SetValueWithoutNotify предотвращает обратное срабатывание TextField при программном заполнении.
5. При отключении View снимаются биндинги и обработчики кнопок/полей; модель сохраняет состояние и подписку на сервис. При включении всё связывается заново.
6. Если UIDocument пересоздаёт rootVisualElement, DocumentView обнаруживает это и восстанавливает привязки.
7. При уничтожении View уничтожается runtime-клон. ViewModel.Disconnect снимает подписки на Model; базовая ViewModel освобождает созданные реактивные свойства.

Runtime-зависимости, подписки и реактивные объекты не сериализуются в assets. ScriptableObject здесь задаёт тип и принадлежность модели экрану, а не служит глобальным каналом событий.

Menu и Match пока используют Tick для источников без единого события изменения, обнаружения смены HeroNet/MatchStateNet и таймеров. R3-подписки используются там, где Model уже публикует состояние. Placement, Loading и Overlay получают данные через события сервисов. Односторонняя зависимость не требует превращать все обновления в общую шину событий.

## Как изменить интерфейс

**Внешний вид:** открыть UXML в UI Builder, изменить компоновку и Interface.uss. Изменение name требует обновить только соответствующий Document-класс.

**Новое значение:** добавить типизированное реактивное свойство в ViewModel; заполнить его из Model; привязать к элементу в BindElements.

**Новая кнопка:** добавить Button в UXML, метод намерения в ViewModel и привязку Button в Document. Игровой сервис предоставляет собственный метод, не принимающий ViewModel.

**Новый экран:** создать ViewModel и Document-класс, затем в редакторе сохранить GameObject с UIDocument, назначенными UXML/PanelSettings, sortingOrder и шаблоном ViewModel. Сохранить префаб, разместить в нужной сцене, передать зависимости из Scope. Фабрика документов в runtime не нужна.

**Предпросмотр:** существующие UXML/USS открываются в UI Builder. Для программного предпросмотра можно использовать prefab документа и менять свойства его runtime ViewModel без подключения сетевых сервисов. Обычный Inspector не сериализует текущие значения R3-свойств.

## Ввод и HP-бары

Пустые слои HUD и HP-бары используют PickingMode.Ignore. Интерактивные панели блокируют начало игровых жестов через EventSystem/PanelRaycaster (FR-IN-06).

MatchViewModel выдаёт список WorldHealthBar с ID игрока, мировым положением и долей здоровья. MatchDocument выбирает активную камеру и проецирует позиции в пространство панели. Бары создаются внутри существующего worldBars; неактивные/погибшие/скрытые герои не отображаются. AR-координаты не передаются по сети.

## Что удалено

UiState/UiParameter, ReactiveDocument, ToolkitRuntime, RuntimePanelOwner, MenuPresenter, MatchPresenter, NetworkMenuView и MatchInterface. Удалены старые State.asset. Общий ActionRequested со строковыми ключами больше не используется.

Сохранённая Editor-команда Game/UI/Configure MVVM documents создаёт/обновляет настроенные префабы, ссылки сцен и шаблоны через Unity Editor API. Она предназначена для миграции структуры, а не для runtime. Перед выполнением сохраняет открытые сцены и восстанавливает набор сцен после работы.

## Проверка

ReactiveUiTests проверяет назначенные assets, порядок документов, типы и независимость runtime-моделей, отсутствие сохранённой runtime-модели в префабах, подавление повторных R3-уведомлений, двустороннюю привязку полей без обратного события, команды установки, отписки при смене источника/уничтожении и восстановление документа.

Проверено 17.11.2026: финальный полный `unity test --mode EditMode` — 397/397 Passed, 0 Failed, 0 Skipped; компиляция без ошибок. Результат — `test-results.xml`.

Play Mode в Unity 6000.6.5f1: Boot → Menu → Training → Menu; создание локального хоста через MenuViewModel, один тестовый серверный участник, готовность/запуск через MatchViewModel, ожидание Placement и подтверждение заглушки, Playing с таймером/HP/золотом/магазином и двумя HP-барами, Results со статистикой, Rematch → Playing → Results → Lobby. Матч завершался ускоренным Advance только в runtime-проверке. После RefreshBindings осталось ровно два HP-бара. Счётчик кадров изменялся; это была работающая игровая петля. Новых ошибок приложения за этот сценарий нет.

В консоли проверочного Editor остались сообщения инструментария: CLI beta.17 запрашивал отсутствующую в Pipeline 0.8 команду editor status; повторная миграция превысила стандартный timeout 5 с, хотя сохранение завершилось. Консоль не очищалась. Эти сообщения не считаются ошибками игровой логики или подтверждением полностью чистой консоли окружения.

Проверка отдельных MPPM-клиентов, мобильной клавиатуры, реальных Android-жестов, двух телефонов, FPS и нагрева остаётся отдельной приёмкой. Android APK после этой MVVM-миграции не пересобирался.
