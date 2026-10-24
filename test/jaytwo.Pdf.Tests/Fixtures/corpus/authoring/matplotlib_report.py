"""Writes matplotlib-report.pdf: a six-page chart report through matplotlib's own PDF backend.

Usage: matplotlib_report.py out.pdf
"""
import math
import site
import sys

sys.path.append(site.getusersitepackages())

import matplotlib

matplotlib.use("pdf")
import matplotlib.pyplot as plt
from matplotlib.backends.backend_pdf import PdfPages

regions = ["North", "South", "East", "West"]
with PdfPages(sys.argv[1], metadata={"Title": "Fleet Utilization Report", "Author": "Example Freight Analytics",
                                      "Subject": "Corpus fixture: matplotlib", "Keywords": "corpus; matplotlib"}) as pdf:
    for page, region in enumerate(regions + ["All regions", "Appendix"]):
        fig, axes = plt.subplots(2, 1, figsize=(8.5, 11))
        days = list(range(1, 91))
        axes[0].plot(days, [60 + 25 * math.sin((d + page * 9) / 9.0) for d in days], label="Utilization %")
        axes[0].set_title(f"{region}: utilization by day")
        axes[0].legend()
        axes[1].bar(regions, [(page + 3) * (i + 2) * 7 % 97 + 20 for i in range(4)], color="#2e75b6")
        axes[1].set_title("Loads by region (synthetic)")
        fig.suptitle(f"Fleet Utilization Report, page {page + 1}")
        pdf.savefig(fig)
        plt.close(fig)
