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
* **Smart Deduplication:** Includes a deterministic fingerprinting system and a `.Deduplicate()` extension to automatically remove double-counted transactions from overlapping report dates.
* **Strict Immutability:** Core domain entities are implemented as C# `records` to prevent accidental side effects.
* **High Precision:** All monetary values use `decimal` to avoid floating-point inaccuracies.

## 🛠️ Getting Started

### Prerequisites
* .NET 10 SDK

### Installation
```bash
git clone https://github.com/filipeamda/Finta.git
cd Finta
dotnet build
```

### Usage Example (Professional "Clean Stream")
```csharp
using Finta.Parsers.Ibkr;
using Finta.Common;

var parser = new IbkrParser();
using var stream1 = File.OpenRead("jan_to_mar.csv");
using var stream2 = File.OpenRead("mar_to_jun.csv");

// Concatenate and deduplicate overlapping reports in one line
var transactions = parser.ParseAsync(stream1)
                         .Concat(parser.ParseAsync(stream2))
                         .Deduplicate();

await foreach (var t in transactions)
{
    Console.WriteLine($"[{t.Date:d}] {t.Type} {t.Ticker}: {t.Quantity} @ {t.Price}");   
}
```

## 🧰 Developer Utilities

### IBKR Anonymizer (`scripts/ibkr/anonymize.py`)
A Python utility to prepare test data safely. It scrubs personal identity and scales all monetary values by a consistent random factor, preserving financial ratios and data integrity while protecting confidentiality.
```bash
python scripts/ibkr/anonymize.py report1.csv report2.csv
```

## 🗺️ Roadmap
- [x] Core Streaming Engine (.NET 10)
- [x] Interactive Brokers (IBKR) Support
- [x] Automated Unit Testing Suite (xUnit)
- [ ] Revolut Business CSV Parser
- [ ] FIFO Capital Gains Engine
- [ ] Export to JSON/Excel/SQL

## ⚖️ License
Distributed under the MIT License. See `LICENSE` for more information.