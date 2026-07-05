# DASHSPEC-ADR-0002: Layout и presentation в spec

## Контекст

Dashboard с 10+ сериями на одном line chart нечитаем: легенда «размазана», линии накладываются.
PlantUML решает **другую** задачу — произвольное позиционирование узлов на плоскости (`left`, `right`, `up`).

Для DashSpec нужны:

1. **Grid layout** — где карточка на дашборде (как panel в Grafana / Metabase).
2. **Chart presentation** — как рисовать данные внутри карточки (legend, лимит серий, высота).

## Решение

### `layout grid` (уровень dashboard)

```text
layout grid {
  columns = 12
  gap = 16
}
```

12-колоночная сетка (по умолчанию `columns=12`, `gap=16`).

### `place` (уровень card)

```text
card "Activity 5-min" {
  place { row = 1 col = 7 span = 6 }
  ...
}
```

| Поле | Смысл |
|------|--------|
| `row` | строка сетки (1-based) |
| `col` | колонка старта (1-based) |
| `span` | ширина в колонках; `full`=12, `half`=6, `third`=4 |

Это **не** PlantUML-граф: нет относительных `left of X`. Только явная сетка — проще парсить и рендерить в CSS Grid.

### Свойства `diagram` (presentation)

Внутри `diagram line|bar`:

```text
legend = bottom | right | hidden
max_series = 6      # top N-1 + Other
height = 360        # px
```

`max_series` агрегирует хвост в серию `Other` по сумме Y.

## Отклонённые варианты

| Вариант | Почему нет |
|---------|------------|
| PlantUML-подобные `left of "Card A"` | Сложный layout solver, хрупкий при добавлении карточек |
| Только CSS в host | Layout не в git/spec, расходится с Metabase→DashSpec целью |
| Авто-legend в host без spec | Магия в C#; разные дашборды хотят разное |

## Следующие шаги (не v0.2)

- `small_multiples` — faceting по `series` вместо одного chart
- `link` между карточками (drill-down)
- responsive overrides (`place @sm { span = full }`)
