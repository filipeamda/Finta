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