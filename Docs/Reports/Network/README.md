# Сетевые измерения

В Editor и Development Build компонент NetworkStatsRecorder автоматически добавляется к PudgeNetworkManager в Boot. После готовности сеанса он запускает запись: прогрев 10 с, запись 60 с, три повтора. В Inspector доступны Scenario, Configured RTT Ms, Configured Loss Percent, Auto Capture и кнопки Start Capture / Stop Capture. Настройки RTT/потерь — подписи стенда, они не включают эмуляцию сети.

После настройки стенда и подключения всех участников задайте одинаковый Scenario на узлах, затем Start Capture для новой серии. Настройки частот и prediction задаются на HeroNet; Mirror sendRate — на менеджере. Не меняйте их внутри серии. Путь к файлам выводится в Console и доступен через LastFile/OutputDirectory: `Application.persistentDataPath/NetworkReports`. На Android копируйте файлы по фактическому пути из лога через adb pull.

Скопируйте CSV всех повторов вместе с `_input.csv` в `Docs/Reports/Network/raw`. Из корня проекта:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Docs/Reports/Network/Summarize-Network.ps1
```

Команда обновляет summary.md. Для другого каталога используйте `-InputDirectory <path> -OutputFile <path>`. ExecutionPolicy меняется только для этого процесса. Самопроверка обработчика на синтетических данных в Temp:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Docs/Reports/Network/Test-NetworkReport.ps1
```

Отчёт проверяет длительности, три полных повтора, неизменность настроек и дубликаты. Средние скорости взвешиваются по длительности интервала; p95 — nearest rank. Отклик считается по событиям companion-файла. Для событий дождитесь остановки героя и сглаживания перед новым тапом.

Mirror bytes/messages учитывают получателей рассылки. Transport payload/batches снимаются с KCP под LatencySimulation и не равны UDP-датаграммам; неизвестный транспорт, UDP packets и наблюдаемые потери обозначаются N/A. Configured loss — введённая настройка, не результат измерения. Host содержит локальный loopback. Commit — HEAD, а не подтверждение чистоты рабочего дерева; player build дополнительно содержит build GUID и внедрённый commit. CSV не содержит токены, IP, ники и payload сообщений.

Без настоящих CSV summary остаётся PENDING. Синтетическая самопроверка не подтверждает качество сети. Полная [ручная приёмка этапа 2](stage_2_acceptance.md).
