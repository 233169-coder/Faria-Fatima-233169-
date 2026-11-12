
import csv
import matplotlib.pyplot as plt

sizes = []
rtt = []

with open("pingpong_results.csv", "r") as file:
    reader = csv.DictReader(file)

    for row in reader:
        sizes.append(int(row["MessageSize"]))
        rtt.append(float(row["MeanRTT_us"]))

plt.figure(figsize=(9, 5))
plt.plot(sizes, rtt, marker="o")

plt.xlabel("Message Size (bytes)")
plt.ylabel("Mean Round-Trip Time (microseconds)")
plt.title("MPI Ping-Pong Benchmark")
plt.grid(True)

plt.xscale("symlog", linthresh=1)
plt.tight_layout()
plt.savefig("pingpong_plot.png", dpi=300)
plt.show()
