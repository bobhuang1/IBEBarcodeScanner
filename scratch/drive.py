"""Drive the IBEBarcodeScanner UI over adb.

Usage:
  python drive.py text                 -> print every visible text node with bounds
  python drive.py tap X Y              -> tap
  python drive.py photo N              -> open the picker (retrying), tap photo cell N, print status
  python drive.py retry-dump           -> dump until a uiautomator dump succeeds
"""
import re
import subprocess
import sys
import time

ADB = r"C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe"
DUMP_PATH = r"C:\temp\ui.xml"


def adb(*args, timeout=60):
    return subprocess.run([ADB, *args], capture_output=True, text=True, timeout=timeout)


def dump(retries=6):
    """uiautomator is flaky on this phone, so retry until we get a well-formed tree."""
    for attempt in range(retries):
        with open(DUMP_PATH, "wb") as fh:
            subprocess.run([ADB, "exec-out", "uiautomator", "dump", "/dev/tty"],
                           stdout=fh, stderr=subprocess.DEVNULL, timeout=60)
        try:
            xml = open(DUMP_PATH, encoding="utf-8", errors="replace").read()
        except OSError:
            xml = ""
        if "</hierarchy>" in xml and len(xml) > 500:
            return xml
        print(f"  (dump attempt {attempt + 1} came back empty, retrying)")
        time.sleep(2)
    return xml


def nodes(xml):
    out = []
    for m in re.finditer(r"<node[^>]*>", xml):
        n = m.group(0)

        def attr(name):
            a = re.search(rf'{name}="([^"]*)"', n)
            return a.group(1) if a else ""

        b = re.search(r'bounds="\[(\d+),(\d+)\]\[(\d+),(\d+)\]"', n)
        rect = tuple(map(int, b.groups())) if b else None
        out.append({
            "text": attr("text"),
            "desc": attr("content-desc"),
            "cls": attr("class").split(".")[-1],
            "clickable": attr("clickable") == "true",
            "rect": rect,
        })
    return out


def texts(xml):
    return [n["text"] for n in nodes(xml) if n["text"].strip()]


def tap(x, y):
    adb("shell", "input", "tap", str(x), str(y))


def launch():
    adb("shell", "am", "force-stop", "net.ibegroup.ibebarcodescanner")
    adb("shell", "monkey", "-p", "net.ibegroup.ibebarcodescanner",
        "-c", "android.intent.category.LAUNCHER", "1")
    time.sleep(7)


def photo_cells(xml):
    """The picker's grid cells, reading order (row by row, left to right)."""
    cells = [n for n in nodes(xml) if n["desc"].startswith("Photo taken on") and n["clickable"]]
    cells.sort(key=lambda n: (n["rect"][1] // 100, n["rect"][0]))
    return cells


def find_button(xml, label):
    for n in nodes(xml):
        if n["clickable"] and n["text"].strip() == label:
            return n
    return None


def main():
    cmd = sys.argv[1] if len(sys.argv) > 1 else "text"

    if cmd == "text":
        for t in texts(dump()):
            print(repr(t)[:100])

    elif cmd == "tap":
        tap(int(sys.argv[2]), int(sys.argv[3]))

    elif cmd == "photo":
        index = int(sys.argv[2]) if len(sys.argv) > 2 else 1
        button = find_button(dump(), "Scan a photo")
        print("Scan a photo button:", button["rect"] if button else "NOT FOUND")
        r = button["rect"]
        tap((r[0] + r[2]) // 2, (r[1] + r[3]) // 2)

        cells = []
        for attempt in range(15):
            time.sleep(2)
            xml = dump()
            cells = photo_cells(xml)
            if cells:
                break
            pkgs = sorted(set(re.findall(r'package="([^"]*)"', xml)))
            print(f"  picker not populated yet (attempt {attempt + 1}), pkg={pkgs}")

        if not cells:
            print("NO PHOTO CELLS FOUND")
            for t in texts(xml):
                print(" ", repr(t)[:90])
            return 1

        print(f"{len(cells)} cells")
        if index > len(cells):
            print(f"cell {index} does not exist")
            return 1

        r = cells[index - 1]["rect"]
        print(f"tapping cell {index} at {(r[0] + r[2]) // 2},{(r[1] + r[3]) // 2} of {cells[index - 1]['desc']}")
        tap((r[0] + r[2]) // 2, (r[1] + r[3]) // 2)

        # Decoding a still on this phone takes seconds, so wait for the status banner to speak.
        for attempt in range(30):
            time.sleep(3)
            xml = dump()
            banner = [n["text"] for n in nodes(xml)
                      if n["rect"] and 470 < n["rect"][1] < 545 and n["text"].strip()]
            print(f"  [{attempt * 3 + 3}s] {banner}")
            if banner and banner[-1] != "Tap Scan to read a barcode.":
                break

        print("--- result ---")
        for t in texts(dump()):
            print(repr(t)[:120])

    return 0


if __name__ == "__main__":
    sys.exit(main())
