"""
IBKR Anonymizer: Batch processes Interactive Brokers CSV reports to scrub identity
and scale monetary values by a global random factor while preserving data integrity
and financial ratios across multiple files.
"""
import csv
import sys
import os
import re
import random
import argparse
import glob

# List of column names that represent monetary values (prices, amounts, P/L, basis, etc.)
# We scale these by the global factor. We DO NOT scale quantities, dates, or multipliers.
MONETARY_COL_NAMES = {
    "Prior Total", "Current Long", "Current Short", "Current Total", "Change",
    "Field Value", "Prior Price", "Current Price", "Mark-to-Market P/L Position",
    "Mark-to-Market P/L Transaction", "Mark-to-Market P/L Commissions",
    "Mark-to-Market P/L Other", "Mark-to-Market P/L Total", "Cost Adj.",
    "Realized S/T Profit", "Realized S/T Loss", "Realized L/T Profit",
    "Realized L/T Loss", "Realized Total", "Unrealized S/T Profit",
    "Unrealized S/T Loss", "Unrealized L/T Profit", "Unrealized L/T Loss",
    "Unrealized Total", "Total", "Securities", "Futures", "Cost Price",
    "Cost Basis", "Close Price", "Value", "Unrealized P/L", "T. Price",
    "C. Price", "Proceeds", "Comm/Fee", "Basis", "Realized P/L", "MTM P/L",
    "Comm in EUR", "MTM in EUR", "Amount", "Tax", "Fee", "Gross Amount",
    "Net Amount", "Gross Rate"
}

# Column names that we should NEVER scale, even if they contain numbers
# (mostly to protect Quantities and Multipliers)
NON_SCALABLE_COL_NAMES = {
    "Quantity", "Mult", "Multiplier", "Conid", "Security ID", "Position", "Prior Quantity", "Current Quantity"
}

def transform_amount(s, factor):
    if not s or s == '--' or s.strip() == '': return s
    
    # Handle percentages (like TWR) - we don't scale percentages
    if '%' in s: return s
    
    is_neg = s.startswith('-')
    # Remove commas and negative signs for parsing
    clean_s = s.replace(',', '').replace('-', '').strip()
    
    try:
        val = float(clean_s)
        new_val = val * factor
        # Format back with reasonable precision
        # Using 6 decimal places but stripping trailing zeros to keep it clean
        res = f"{new_val:.6f}".rstrip('0').rstrip('.')
        if is_neg: res = '-' + res
        return res
    except ValueError:
        return s

def anonymize_batch(input_paths, factor):
    for input_path in input_paths:
        if not os.path.exists(input_path):
            print(f"Skipping: {input_path} (File not found)")
            continue

        output_path = input_path.replace(".csv", "-anonymized.csv")
        if output_path == input_path:
            output_path = input_path + ".anonymized.csv"

        print(f"Processing: {input_path} -> {output_path} (Factor: {factor:.3f})")

        header_maps = {} # Maps section_name -> {col_name -> index}

        with open(input_path, 'r', encoding='utf-8') as fin, \
             open(output_path, 'w', encoding='utf-8', newline='') as fout:
            
            reader = csv.reader(fin)
            writer = csv.writer(fout)
            
            for row in reader:
                if not row or len(row) < 2:
                    writer.writerow(row)
                    continue
                
                section = row[0]
                row_type = row[1]
                
                # 1. Capture Headers for dynamic mapping
                if row_type == "Header":
                    header_maps[section] = {name.strip(): i for i, name in enumerate(row)}
                    writer.writerow(row)
                    continue

                mapping = header_maps.get(section, {})

                # 2. Scrub Identity (uses dynamic mapping if available)
                if section in ["Account Information", "Statement", "Account Information (Cont.)"] and row_type == "Data":
                    field_name_idx = mapping.get("Field Name", 2)
                    field_val_idx = mapping.get("Field Value", 3)
                    
                    if len(row) > max(field_name_idx, field_val_idx):
                        field_name = row[field_name_idx]
                        if field_name == "Name":
                            row[field_val_idx] = "Test User"
                        elif field_name == "Account":
                            row[field_val_idx] = "U1234567"
                        elif field_name == "BrokerAddress":
                            row[field_val_idx] = "123 Test St, Anonymized City"

                # 3. Scrub Fee Descriptions (uses dynamic mapping)
                if section == "Fees" and row_type == "Data":
                    desc_idx = mapping.get("Description", 5)
                    if len(row) > desc_idx:
                        row[desc_idx] = re.sub(r'P\*+ME', 'ACCOUNT_SCRUBBED', row[desc_idx])
                        row[desc_idx] = re.sub(r'[A-Z0-9]{5,}:', 'ACCOUNT_SCRUBBED:', row[desc_idx])

                # 4. Scale Monetary Values (uses dynamic mapping)
                if row_type in ["Data", "Total", "SubTotal", "Summary"]:
                    # Iterate through the mapping to find columns to scale
                    for col_name, col_idx in mapping.items():
                        if col_idx < len(row):
                            # Rule: Scale if it's in the monetary whitelist AND NOT in the blacklist
                            if col_name in MONETARY_COL_NAMES and col_name not in NON_SCALABLE_COL_NAMES:
                                # Special case for Change in NAV: only scale specific rows
                                if section == "Change in NAV" and col_name == "Field Value":
                                    field_name_idx = mapping.get("Field Name", 2)
                                    if field_name_idx < len(row):
                                        field_name = row[field_name_idx]
                                        # Only scale numeric NAV components
                                        if field_name in ["Starting Value", "Mark-to-Market", "Deposits & Withdrawals", "Dividends", "Withholding Tax", "Change in Dividend Accruals", "Commissions", "Sales Tax", "Other FX Translations", "Ending Value"]:
                                            row[col_idx] = transform_amount(row[col_idx], factor)
                                else:
                                    row[col_idx] = transform_amount(row[col_idx], factor)

                writer.writerow(row)

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Anonymize IBKR CSV reports using a global scaling factor and dynamic header mapping.")
    parser.add_argument("files", nargs="+", help="One or more CSV files to anonymize.")
    parser.add_argument("--factor", type=float, help="Optional specific factor. If omitted, a random factor between 0.3 and 9.0 is used.")
    
    args = parser.parse_args()
    
    # Expand wildcards (glob) manually in case the shell didn't do it
    expanded_files = []
    for f in args.files:
        matches = glob.glob(f)
        if matches:
            expanded_files.extend(matches)
        else:
            expanded_files.append(f)

    # Generate factor once for the entire batch
    if args.factor:
        scaling_factor = args.factor
    else:
        scaling_factor = round(random.uniform(0.3, 9.0), 3)
    
    anonymize_batch(expanded_files, scaling_factor)
    print(f"\nBatch Complete. Global Scaling Factor used: {scaling_factor}")
