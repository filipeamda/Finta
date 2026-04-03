# ⚽ Finta

**Finta** is a high-performance, modular financial engine built on **.NET 10**. Designed to "dribble" through the complexity of fragmented broker reports, it provides a unified, streaming-first ingestion layer and a robust calculation core.

![.NET Version](https://img.shields.io/badge/.NET-10.0-blueviolet)
![License](https://img.shields.io/badge/license-MIT-green)
![Build](https://img.shields.io/badge/build-passing-brightgreen)

## 🚀 The Vision
Most retail investment data is locked behind messy, inconsistent CSV exports. **Finta** solves this by providing a standardized, high-precision domain model and a pluggable parser architecture. Whether it's a 5,000-row IBKR export or a complex crypto transaction history, Finta processes it with a near-zero memory footprint.

## 🏗️ Architecture
Finta is built as a **Modular Monolith**, ensuring strict separation of concerns and allowing for independent NuGet distribution of individual parsers.

| Project | Responsibility | Dependencies |
| :--- | :--- | :--- |
| **Finta.Common** | The "Brain." Defines immutable domain models (`Transaction`) and parser contracts. | None |
| **Finta.Parsers.Ibkr** | The "Ingestor." Specialized logic for streaming Interactive Brokers reports. | `CsvHelper` |
| **Finta.Engine** | The "Logic." (Planned) FIFO/LIFO capital gains, FX normalization, and performance metrics. | `Finta.Common` |

## ⚡ Key Technical Features
* **Streaming-First Ingestion:** Uses `IAsyncEnumerable<T>` and `Stream` to ensure **O(1) memory complexity**. It processes massive files without significant RAM allocation.
* **Strict Immutability:** Core domain entities are implemented as C# `records` to prevent accidental side effects during complex financial calculations.
* **High Precision:** All monetary values use `decimal` to avoid floating-point inaccuracies.
* **Modern .NET 10 Stack:** Leverages the latest C# features and the new `.slnx` solution format for cleaner version control.

## 🛠️ Getting Started

### Prerequisites
* .NET 10 SDK

### Installation
```bash
git clone https://github.com/filipeamda/Finta.git
cd Finta
dotnet build
```

### Usage Example
```csharp
using Finta.Parsers.Ibkr;

var parser = new IbkrParser();
using var stream = File.OpenRead("path/to/your/ibkr_report.csv");

await foreach (var transaction in parser.ParseAsync(stream))
{
    Console.WriteLine($"Processed {transaction.Ticker}: {transaction.Quantity} units");   
}
```

## 🗺️ Roadmap
- [ ] Core Streaming Engine (.NET 10)
- [ ] Interactive Brokers (IBKR) Support
- [ ] Automated Unit Testing Suite (xUnit)
- [ ] Revolut Business CSV Parser
- [ ] FIFO Capital Gains Engine
- [ ] Export to JSON/Excel/SQL

## ⚖️ License
Distributed under the MIT License. See `LICENSE` for more information.