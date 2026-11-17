# Базовый стиль C#

Утверждённый стиль проекта. Корневой `AGENTS.md` требует применять эти правила при создании и изменении C#-кода. Пример ниже служит образцом оформления; архитектурные требования проекта по-прежнему задаются в `AGENTS.md` и ТДД.

## Оформление

- Отступ — четыре пробела, без табуляции.
- Открывающая фигурная скобка — на отдельной строке (Allman).
- У `if`, `else`, циклов и `using` всегда есть фигурные скобки, даже для одной инструкции.
- Одна инструкция на строку; каждое поле объявляется отдельно.
- Между методами и самостоятельными логическими шагами — одна пустая строка.
- Длинные списки аргументов переносятся по одному аргументу на строку.
- Простые свойства и короткие вычисления допускают `=>`. Методы с проверками и изменением состояния используют обычное тело.
- Вложенные тернарные выражения не используются. Сложные условия лучше разбить или назвать отдельной переменной.

## Именование и структура

- Типы, методы и свойства — `PascalCase`.
- Интерфейсы — `IPascalCase`.
- Параметры и локальные переменные — `camelCase`.
- Приватные поля — `_camelCase`; константы — `PascalCase`.
- Модификаторы доступа указываются явно.
- Поля, которые не переназначаются после конструктора, — `readonly`.
- Для этого проекта: блочные `namespace`, классы по умолчанию `sealed`.
- Рекомендуемый порядок членов: константы, поля, конструкторы, свойства, публичные методы, приватные методы. Связанные члены можно группировать вместе.
- `var` используется, когда тип очевиден из правой части, например при `new`. В остальных случаях предпочтителен явный тип.
- Комментарии объясняют причину или ограничение, а не повторяют код. Комментарии и идентификаторы — на английском.

## Пример

Учебный пример сервиса внутри одного процесса. Он показывает оформление и не предлагает менять сетевую архитектуру или экономику игры.

```csharp
using System;
using System.Collections.Generic;

namespace Example.Services
{
    public interface IWallet
    {
        bool TrySpend(int amount);
    }

    public sealed class PurchaseService
    {
        private const int MaximumItemCount = 10;

        private readonly IWallet _wallet;
        private readonly List<string> _purchasedItems = new();

        public PurchaseService(IWallet wallet)
        {
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
        }

        public int PurchasedItemCount => _purchasedItems.Count;

        public bool TryPurchase(string itemId, int price)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                throw new ArgumentException(
                    "Item identifier must not be empty.",
                    nameof(itemId));
            }

            if (price <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(price),
                    price,
                    "Price must be positive.");
            }

            bool hasReachedItemLimit = PurchasedItemCount >= MaximumItemCount;

            if (hasReachedItemLimit || HasPurchased(itemId))
            {
                return false;
            }

            if (!_wallet.TrySpend(price))
            {
                return false;
            }

            _purchasedItems.Add(itemId);

            return true;
        }

        private bool HasPurchased(string itemId)
        {
            foreach (string purchasedItem in _purchasedItems)
            {
                if (string.Equals(purchasedItem, itemId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
```

В реальном коде интерфейс и класс обычно размещаются в отдельных файлах. Здесь они приведены вместе, чтобы пример читался целиком.

Это выбранный командой вариант оформления C#, а не обязательный стандарт языка. При изменении предпочтений команды нужно обновить этот документ и соответствующие инструкции в `AGENTS.md`.
