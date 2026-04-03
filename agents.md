# Agent Context: Project Finta

## 🎯 Project Vision
Finta is a high-performance, modular financial engine built with **.NET 10**. It is designed to ingest, sanitize, and analyze disparate broker data (starting with Interactive Brokers) using a streaming-first approach to minimize memory footprint.

## 🏗️ Architecture: Modular Monolith
- **Finta.Common**: The Domain layer. Contains immutable records (`Transaction`), enums, and core abstractions (`IExchangeParser`).
- **Finta.Parsers.Ibkr**: The Infrastructure layer. Implements specific logic for Interactive Brokers CSV reports.
- **Finta.Engine** (Planned): The Logic layer. Handles FIFO calculations, FX normalization, and performance metrics.

## 🛠️ Technical Stack & Standards
- **Framework**: .NET 10 (C# 14/15).
- **Inversion of Control**: Strategy Pattern for parsers.
- **Performance**: Mandatory use of `IAsyncEnumerable<T>` and `Stream` for I/O to ensure O(1) memory complexity during ingestion.
- **Data Integrity**: Use of `decimal` for all financial values; `records` for immutability.
- **Libraries**: `CsvHelper` for robust CSV processing.

## 📜 Coding Conventions
- File-scoped namespaces.
- Primary constructors where applicable.
- Explicit `CultureInfo.InvariantCulture` for all parsing.
- Strict separation between "Domain Models" and "Parser Mapping Logic."

## 🧩 Developing New Parsers
- **TDD First**: Every parser MUST have corresponding CSV samples in a `TestData` directory within the test project.
- **Structural Detection**: Always use a "Structural Factory" pattern. Identify report formats by checking column header signatures (`DetectFormatAsync`) rather than file names.
- **Dynamic Tests**: Tests must discover all files in `TestData` and run as a `[Theory]` to ensure robustness.
- **Streaming Mandatory**: Use `CsvHelper` with `csv.ReadAsync()` to process rows one-by-one. Never load the entire file into memory.
- **Deduplication**: Parsers should provide a `.Deduplicate()` extension method in `Finta.Common` that uses `Transaction.Fingerprint` to handle overlapping report dates.
- **Error Handling**: Throw `UnrecognizedExchangeFormatException` if a file signature is not recognized.
- **Data Mapping Conventions**:
  - `Buy`: `Quantity` > 0.
  - `Sell`: `Quantity` < 0.
  - `Dividend`: `Quantity` > 0 (cash inflow).
  - `Tax/Fee`: `Quantity` < 0 (cash outflow).
  - `Price`: Unit price of the asset.
  - `Ticker`: For non-trade rows, extract the ticker from the description when possible.
- **Raw Data Integrity**: The `RawData` property of `Transaction` must store the original CSV row for auditability.