# DASHSPEC-ADR-0020: Card `ref` and tab layout board

## Контекст

Длинные `card stakeholder_peak_by_app` неудобны в layout. Metabase-стиль ASCII-сетки (`[Q E] / [T F]`) читается в git лучше, чем только `place { row col span }` на каждой карточке.

ADR-0002 вводил `layout grid` (dashboard) и `place` (card). Нужен компактный tab-level layout без PlantUML-solver.

## Решение

### `ref` на карточке

Короткий псевдоним для layout; `card` id остаётся каноническим для логов и bind.

```text
card stakeholder_peak_by_app as "№1 Пик …" ref Q {
  ...
}
```

`ref` — опционально, после `as "Title"`, до `{`.

### Layout board на вкладке

Внутри `tab … { }` (или `@tab` module `tab … { }`):

```text
tab stakeholder as "Отчёты" {
  layout {
    [ Q W ]
    [ E ]
    [ R ]
    [ T ]
  }
}
```

Строки — `[ … ]`, ячейки — `ref` или `card` id через пробел.

**Размер сетки (информационно):**

| Метрика | Правило |
|---------|---------|
| строки | число строк `[ … ]` |
| колонки | `max(ячеек в строке)` по всем строкам |

Пример `[ Q E ]` / `[ T F ]` → 2 строки × 2 колонки.

**Строки с разным числом ячеек** — каждая строка считается отдельно (не жёсткая N-колоночная матрица):

```text
layout {
  [ Q E ]
  [ R T Y ]
  [ F ]
}
```

→ 3 строки, `ColumnCount = max(2, 3, 1) = 3` (метаданные). На `columns = 12`:

| строка | ячейки | span | col |
|--------|--------|------|-----|
| 1 | Q, E | 6 | 1, 7 |
| 2 | R, T, Y | 4 | 1, 5, 9 |
| 3 | F | 12 (full) | 1 |

Q и E не «привязаны» к трети ширины под R — они занимают половину строки 1, R/T/Y — треть строки 2.

### Маппинг на `layout grid`

На фоне `layout grid { columns = 12 }`:

| Ячеек в строке | span | col |
|----------------|------|-----|
| 1 | `columns` (full) | 1 |
| N | `columns / N` | `1 + i * span` |

Строка board → `row` (1-based).

### Приоритет

1. `place { … }` на карточке — переопределяет board
2. `layout { [ … ] }` на tab
3. `TabLayoutCompactor` (порядок карточек + эвристики по kind)

### Валидация

- `ref` уникален среди карточек вкладки
- каждая карточка вкладки ровно один раз в board (если board задан)
- токен в board резолвится в `ref` или `id`

`tab … cards { Q W }` допускает ref в списке (резолв при assign).

## Отклонённые варианты

| Вариант | Почему нет |
|---------|------------|
| Только markdown `\| Q \| W \|` | хуже парсится, смешение с rich text |
| `layout columns = 2 { Q W / E }` | менее наглядно, чем коробочки |
| PlantUML `left of` | см. ADR-0002 |

## Связанные ADR

- ADR-0002 — `layout grid`, `place`
- ADR-0011 — `@tab` modules; board в `tab { layout { … } }`
