#!/usr/bin/env python3
"""
Coverage reporter: fusiona los reports cobertura.xml de coverlet de todas las
suites, normaliza rutas duplicadas y excluye código generado (migraciones EF,
source generators). No maquilla: todo lo excluido se lista al final.

Uso:
    dotnet test DotnetTCG.sln --collect:"XPlat Code Coverage" --results-directory /tmp/cov
    python3 scripts/coverage.py /tmp/cov
"""
import glob
import os
import re
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict

PROJECTS = ("DotnetTCG.Domain", "DotnetTCG.Application", "DotnetTCG.Infrastructure", "DotnetTCG.Api")

# Código generado: no escribe nadie a mano, no tiene comportamiento propio que probar.
EXCLUDE = (
    re.compile(r"/Migrations/"),
    re.compile(r"\.Designer\.cs$"),
    re.compile(r"ModelSnapshot\.cs$"),
    re.compile(r"/obj/"),
    re.compile(r"\.generated\.cs$"),
)


def normalize(filename: str) -> str | None:
    """Devuelve (assembly, ruta relativa al proyecto) o None si no es src."""
    for proj in PROJECTS:
        marker = proj + "/"
        if marker in filename:
            asm = proj.replace("DotnetTCG.", "")
            return asm, filename.split(marker, 1)[1]
        # rutas relativas ya sin prefijo (formato alternativo de reporte):
        if not filename.startswith("/") and not filename.startswith(proj):
            pass
    return None


def rel_path(filename: str, source_root: str) -> str | None:
    """Normaliza a '<Assembly>/<rel>' usando el source root del reporte."""
    full = filename if filename.startswith("/") else source_root.rstrip("/") + "/" + filename
    for proj in PROJECTS:
        marker = "/" + proj + "/"
        if marker in full:
            rel = full.split(marker, 1)[1]
            return proj.replace("DotnetTCG.", "") + "/" + rel
    return None


def main() -> int:
    results_dir = sys.argv[1] if len(sys.argv) > 1 else "/tmp/cov"
    line_hits: dict[tuple[str, str], int] = {}
    excluded: set[tuple[str, str]] = set()

    files = glob.glob(os.path.join(results_dir, "**", "coverage.cobertura.xml"), recursive=True)
    if not files:
        print(f"No reports in {results_dir}", file=sys.stderr)
        return 1

    for f in files:
        root = ET.parse(f).getroot()
        source_el = root.find("sources/source")
        source_root = source_el.text or "" if source_el is not None else ""
        for cls in root.iter("class"):
            rel = rel_path(cls.get("filename", ""), source_root)
            if rel is None:
                continue
            if any(p.search(rel) for p in EXCLUDE):
                excluded.add(rel)
                continue
            for line in cls.iter("line"):
                key = (rel, line.get("number", "?"))
                hits = int(line.get("hits", "0"))
                line_hits[key] = max(line_hits.get(key, 0), hits)

    per_file: dict[str, list[int]] = defaultdict(lambda: [0, 0])  # [cov, total]
    per_asm: dict[str, list[int]] = defaultdict(lambda: [0, 0])
    for (rel, _), hits in line_hits.items():
        asm = rel.split("/", 1)[0]
        pf, pa = per_file[rel], per_asm[asm]
        pf[1] += 1
        pa[1] += 1
        if hits > 0:
            pf[0] += 1
            pa[0] += 1

    print("== Cobertura por assembly (mergeado, sin código generado) ==")
    total = [0, 0]
    for asm in ("Domain", "Application", "Infrastructure", "Api"):
        cov, valid = per_asm[asm]
        pct = 100 * cov / max(valid, 1)
        print(f"  {asm:15} {pct:5.1f}%  {cov}/{valid}   sin cubrir: {valid - cov}")
        total[0] += cov
        total[1] += valid
    pct = 100 * total[0] / total[1]
    print(f"  {'GLOBAL':15} {pct:5.1f}%  {total[0]}/{total[1]}   sin cubrir: {total[1] - total[0]}")

    rows = sorted(
        ((lc - cov, lc, rel) for rel, (cov, lc) in per_file.items() if lc > 0),
        key=lambda r: -r[0],
    )
    print("\n== Top 20 archivos por líneas sin cubrir ==")
    for uncov, total_lines, rel in rows[:20]:
        print(f"  {uncov:4} uncov / {total_lines:4}  {rel}")

    if excluded:
        print("\n== Excluido (código generado, trazado completo) ==")
        for rel in sorted(excluded):
            print(f"  {rel}")

    return 0


if __name__ == "__main__":
    sys.exit(main())
