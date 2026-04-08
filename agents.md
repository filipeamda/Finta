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

## 📋 Logging Requirements
All production code **MUST** implement structured logging using `Microsoft.Extensions.Logging.ILogger<T>` to provide visibility into data quality, processing issues, and operational milestones.

### Logging Levels
- **Error** (`LogError`): Operation failed. _Example: Validation failed, resource not found, constraint violation._
- **Warning** (`LogWarning`): Issue detected but operation recovered with fallback/default. Indicates potential problem. _Example: Missing field → defaulted to standard value._
- **Information** (`LogInformation`): Significant milestones and workflow completion. _Example: Operation started, completed with result counts._

### Best Practices for Structured Logging

**1. Dependency Injection**
Inject `ILogger<T>` into your classes via the constructor. This ensures testability and idiomatic .NET lifecycle management.

**2. Performance (LoggerMessage Source Generators)**
For high-frequency loops or large file ingestion, use the `[LoggerMessage]` source generator attribute. This avoids boxing and improves throughput by pre-compiling log templates.

**3. Use Named Parameters with PascalCase**
```csharp
// ✅ Good: Aggregators can search/group by field name
logger.LogError("Processing failed for resource {ResourceId}: {ErrorDescription}. Constraint: {Constraint}", 
    resourceId, errorDesc, "non-null");

// ❌ Bad: String interpolation loses structure
logger.LogError($"Processing failed for {resourceId}: {errorDesc}");
```

**4. Include Contextual Details**
Always include:
- **ResourceId**: What entity/item was being processed
- **FieldName**: Which field/component failed (if applicable)
- **ActualValue**: What was found
- **ExpectedFormat/Constraint**: What was required

```csharp
logger.LogWarning("Field missing, applying default. Resource {ResourceId}, Field {FieldName}, Default {DefaultValue}", 
    resourceId, "Currency", "USD");
```

**5. Keep Message Templates Constant**
The template itself should never vary (for log aggregator grouping):
```csharp
// ✅ Good: Template is constant; only parameters change
foreach (var item in items)
    logger.LogError("Invalid value. Resource {ResourceId}, Field {FieldName}, Value {Value}", item.Id, field, value);

// ❌ Bad: Template changes per iteration
if (count < 5)
    logger.LogError("Error processing {ResourceId}", id);
else
    logger.LogError("Another error processing {ResourceId}", id);
```

**6. Log Before Major Transitions**
Log decisions and state changes at operation boundaries:
```csharp
logger.LogInformation("Starting calculation. PortfolioId {PortfolioId}, Method {CalculationMethod}", portfolioId, method);

// ... work ...

logger.LogInformation("Calculation completed. PortfolioId {PortfolioId}, ResultCount {ResultCount}", portfolioId, results.Count);
```

**7. Use Consistent Parameter Names Across the Codebase**
- `{LineNumber}` for CSV/file line references
- `{ResourceId}` or `{TransactionId}` for domain entities
- `{FieldName}` for structured data fields
- `{ActualValue}` and `{ExpectedValue}` for mismatches

**8. Never Log PII or Sensitive Data**
Do not log personally identifiable information or sensitive financial data in production:
- Account numbers or statement references
- Full names or identities
- Specific transaction amounts (log counts/metadata only)
- Passwords or authentication tokens