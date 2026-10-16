#!/usr/bin/env python3
"""Render Mermaid sources in ./diagrams to SVG and PNG.

Два пути рендеринга, первый доступный побеждает:

1. **Локальный mermaid-cli** (`mmdc`) — рекомендуется: работает без интернета,
   даёт стабильный результат.
       npm install --prefix "$env:TEMP\\mmdc" @mermaid-js/mermaid-cli
   Скрипт найдёт `mmdc` в PATH или в `%TEMP%\\mmdc\\node_modules\\.bin`.
   Для отрисовки используется установленный Chrome/Edge (Chromium скачивать не нужно).

2. **Публичный сервис Kroki** (https://kroki.io) — запасной вариант, если Node нет.
   Требует доступа в интернет; сервис изредка отвечает ошибкой, поэтому есть повторы.

PNG рендерится с двойным масштабом: такие картинки хорошо смотрятся в DOCX и презентациях.

Запуск из корня репозитория:
    python docs/architecture/render-diagrams.py
"""

from __future__ import annotations

import json
import os
import pathlib
import re
import shutil
import subprocess
import sys
import time
import urllib.error
import urllib.request

KROKI = "https://kroki.io/mermaid/svg"
USER_AGENT = (
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
    "(KHTML, like Gecko) Chrome/131.0 Safari/537.36"
)
SCALE = 2

BROWSER_PATHS = [
    r"C:\Program Files\Google\Chrome\Application\chrome.exe",
    r"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
    r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
    "/usr/bin/google-chrome",
    "/usr/bin/chromium",
    "/usr/bin/chromium-browser",
]

DIAGRAMS = pathlib.Path(__file__).resolve().parent / "diagrams"


# --------------------------------------------------------------------------- #
# Поиск инструментов
# --------------------------------------------------------------------------- #

def find_browser() -> str | None:
    for path in BROWSER_PATHS:
        if pathlib.Path(path).exists():
            return path
    for name in ("google-chrome", "chromium", "chrome", "msedge"):
        found = shutil.which(name)
        if found:
            return found
    return None


def find_mmdc() -> str | None:
    found = shutil.which("mmdc")
    if found:
        return found
    candidates = [
        pathlib.Path(os.environ.get("TEMP", "/tmp")) / "mmdc" / "node_modules" / ".bin" / "mmdc.cmd",
        pathlib.Path(os.environ.get("TEMP", "/tmp")) / "mmdc" / "node_modules" / ".bin" / "mmdc",
        pathlib.Path(__file__).resolve().parent / "node_modules" / ".bin" / "mmdc.cmd",
    ]
    for candidate in candidates:
        if candidate.exists():
            return str(candidate)
    return None


def puppeteer_config(browser: str) -> pathlib.Path:
    """mermaid-cli рисует через Puppeteer; указываем уже установленный браузер."""
    path = DIAGRAMS / "_puppeteer.json"
    path.write_text(
        json.dumps({"executablePath": browser, "args": ["--no-sandbox", "--disable-gpu"]}),
        encoding="utf-8",
    )
    return path


# --------------------------------------------------------------------------- #
# Путь 1: локальный mermaid-cli
# --------------------------------------------------------------------------- #

def render_local(mmdc: str, config: pathlib.Path, source: pathlib.Path) -> None:
    mermaid_config = DIAGRAMS / "mermaid-config.json"
    extra_config = ["-c", str(mermaid_config)] if mermaid_config.exists() else []
    base = ["-i", str(source), "-b", "white", "-p", str(config), *extra_config, "-q"]
    for suffix, extra in ((".svg", []), (".png", ["-s", str(SCALE)])):
        target = source.with_suffix(suffix)
        result = subprocess.run(
            [mmdc, *base, "-o", str(target), *extra],
            capture_output=True,
            text=True,
            timeout=300,
        )
        if result.returncode != 0 or not target.exists():
            raise SystemExit(
                f"mmdc не смог отрисовать {source.name} ({suffix}):\n"
                f"{result.stdout[-800:]}\n{result.stderr[-800:]}"
            )


# --------------------------------------------------------------------------- #
# Путь 2: публичный Kroki + headless-браузер
# --------------------------------------------------------------------------- #

def kroki_svg(source: bytes, attempts: int = 6) -> bytes:
    last_error = ""
    for attempt in range(1, attempts + 1):
        request = urllib.request.Request(
            KROKI,
            data=source,
            method="POST",
            headers={
                "Content-Type": "text/plain",
                "User-Agent": USER_AGENT,
                "Accept": "image/svg+xml,*/*",
            },
        )
        try:
            with urllib.request.urlopen(request, timeout=180) as response:
                return response.read()
        except urllib.error.HTTPError as error:
            last_error = f"{error.code}: {error.read()[:200].decode('utf-8', 'replace')}"
        except OSError as error:
            last_error = f"{type(error).__name__}: {error}"
        print(f"  попытка {attempt}/{attempts}: {last_error[:100]}")
        time.sleep(3 * attempt)
    raise SystemExit(f"Kroki недоступен: {last_error}")


def render_kroki(browser: str, source: pathlib.Path) -> None:
    svg_text = kroki_svg(source.read_bytes()).decode("utf-8")
    svg_path = source.with_suffix(".svg")
    svg_path.write_text(svg_text, encoding="utf-8")

    width = int(float(re.search(r'width="([\d.]+)"', svg_text).group(1)))
    height = int(float(re.search(r'height="([\d.]+)"', svg_text).group(1)))
    uri = "file:///" + str(svg_path.resolve()).replace("\\", "/")
    png_path = source.with_suffix(".png")
    subprocess.run(
        [
            browser,
            "--headless=new",
            "--disable-gpu",
            "--no-sandbox",
            "--hide-scrollbars",
            "--default-background-color=FFFFFFFF",
            f"--force-device-scale-factor={SCALE}",
            f"--window-size={width},{height}",
            f"--screenshot={png_path}",
            uri,
        ],
        check=False,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
        timeout=300,
    )
    if not png_path.exists():
        raise SystemExit(f"Браузер не создал {png_path.name}")


# --------------------------------------------------------------------------- #

def main() -> int:
    sources = sorted(DIAGRAMS.glob("*.mmd"))
    if not sources:
        raise SystemExit(f"В {DIAGRAMS} нет файлов *.mmd")

    browser = find_browser()
    mmdc = find_mmdc()
    if mmdc:
        if browser is None:
            raise SystemExit(
                "Найден mmdc, но не найден Chrome/Edge. "
                "Укажите путь в BROWSER_PATHS."
            )
        config = puppeteer_config(browser)
        mode = f"mermaid-cli ({mmdc})"
    elif browser:
        mode = "Kroki + headless-браузер"
    else:
        raise SystemExit(
            "Нужен либо mermaid-cli, либо Chrome/Edge. "
            "Установка: npm install -g @mermaid-js/mermaid-cli"
        )

    print(f"Режим рендеринга: {mode}")
    for source in sources:
        if mmdc:
            render_local(mmdc, config, source)
        else:
            render_kroki(browser, source)
        print(
            f"  {source.name}: "
            f"svg={source.with_suffix('.svg').stat().st_size} B, "
            f"png={source.with_suffix('.png').stat().st_size} B"
        )
        if not mmdc:
            time.sleep(1.5)

    if mmdc:
        config.unlink(missing_ok=True)
    print(f"Готово: {len(sources)} диаграмм")
    return 0


if __name__ == "__main__":
    sys.exit(main())
