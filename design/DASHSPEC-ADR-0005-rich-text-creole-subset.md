# DASHSPEC-ADR-0005: Rich text — Creole-subset (не PlantUML engine)

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-06-29 |
| **Relates to** | [ADR-0004](DASHSPEC-ADR-0004-diagram-column-as.md), PlantUML Creole |

## Контекст

PlantUML использует встроенный Creole для подписей, но движок — Java, вывод в SVG (`Sheet`/`Stripe`), без standalone-библиотеки. Полная совместимость потребовала бы `plantuml.jar` или портирования половины PlantUML.

В DashSpec уже есть PlantUML-подобный `as "Label"`. Дизайнерам иногда нужен акцент в **статическом** тексте spec: заголовок карточки, подписи осей, legend — без разметки в данных SQL.

## Решение

### Creole-subset в Core (`CreoleSubset.ToHtml`)

Свой лёгкий парсер в `DashSpec.Core` — **не** PlantUML Creole 1:1. HTML-encode всего текста, затем безопасные inline-теги.

| Синтаксис | HTML |
|-----------|------|
| `**bold**` | `<strong>` |
| `//italics//` | `<em>` |
| `""mono""` | `<code>` |
| `__underline__` | `<u>` |
| `--strike--` | `<s>` |
| `<color:blue>…</color>` | `<span style="color:…">` |
| `<back:orange>…</back>` | `<span style="background:…">` |

Цвет: `#rgb` / `#rrggbb` или whitelist имён (`blue`, `red`, `green`, `orange`, `gray`, …). Сырой HTML из spec **запрещён**.

Ограничения v0.3: без вложенности, без списков `*`, без иконок `<&code>`, без Markdown.

### Whitelist полей (где парсится Creole)

| Поле | Пример |
|------|--------|
| `card id as "…"` title | `№2 **Peak apps** per user` |
| `legend { min max title }` | `max = "макс. **{max}**"` |
| `diagram … as "…"` (`*_as`) | `value = x as "//Разных// ПО"` |

**Не** парсится: значения осей/tooltip из SQL, `peak_apps`, числа в ячейках — только `LabelFormat`.

Плейсхолдеры `{min}` / `{max}` в legend подставляются **до** Creole (см. `MatrixPresentation.FormatLegend*`).

### Host

Компонент `RichTextView` рендерит `MarkupString` из `CreoleSubset.ToHtml`. Используется в заголовке карточки, legend heatmap, подписях осей из `*_as`.

## Отклонённые варианты

| Вариант | Почему нет |
|---------|------------|
| PlantUML Creole engine | Java, SVG pipeline, нет NuGet |
| Markdown (Markdig) | другой синтаксис; PlantUML-метафора с `as` |
| Creole на tooltip из БД | данные, не spec; остаётся `tooltip_format = list` |
| Полный HTML Creole PlantUML | XSS, сложность |

## Последствия

- Дизайнеры могут акцентировать статический copy в `.dashspec` без правки Host.
- Новые конструкции Creole — расширение `CreoleSubset` + тесты, не registry.
- При необходимости позже: `@format markdown` на отдельных блоках (отдельный ADR).
