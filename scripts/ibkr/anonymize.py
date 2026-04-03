import csv
import sys
import os
import re

def is_number(s):
    if not s: return False
    s = s.replace(',', '')
    try:
        float(s)
        return True
    except ValueError:
        return False

def transform_amount(s, factor=1/3):
    if not s or s == '--': return s
    is_neg = s.startswith('-')
    clean_s = s.replace(',', '').replace('-', '')
    try:
        val = float(clean_s)
        new_val = val * factor
        # Format back with same precision or at least some reasonable one
        res = f"{new_val:f}".rstrip('0').rstrip('.')
        if is_neg: res = '-' + res
        return res
    except ValueError:
        return s

def anonymize(input_path, output_path):
    with open(input_path, 'r', encoding='utf-8') as fin, \
         open(output_path, 'w', encoding='utf-8', newline='') as fout:
        
        reader = csv.reader(fin)
        writer = csv.writer(fout)
        
        for row in reader:
            if not row:
                writer.writerow(row)
                continue
            
            section = row[0]
            row_type = row[1] if len(row) > 1 else ""
            
            # Anonymize Identity
            if section == "Account Information" and row_type == "Data":
                field_name = row[2]
                if field_name == "Name":
                    row[3] = "Test User"
                elif field_name == "Account":
                    row[3] = "U1234567"
            
            if section == "Statement" and row_type == "Data" and row[2] == "BrokerAddress":
                row[3] = "123 Test St, Anonymized City"

            # Scrub specific fields in Fees
            if section == "Fees" and row_type == "Data":
                row[5] = re.sub(r'P\*+ME', 'ACCOUNT_SCRUBBED', row[5])

            # Multiply Amounts by 1000
            if row_type in ["Data", "Total", "SubTotal", "Summary"]:
                if section == "Net Asset Value":
                    for i in range(3, 8): 
                        if i < len(row): row[i] = transform_amount(row[i])
                elif section == "Change in NAV":
                    if len(row) > 3: row[3] = transform_amount(row[3])
                elif section == "Mark-to-Market Performance Summary":
                    # P/L columns: 8, 9, 10, 11, 12. Skip if symbol/category
                    for i in range(8, 13):
                        if i < len(row): row[i] = transform_amount(row[i])
                elif section == "Realized & Unrealized Performance Summary":
                    for i in range(4, 16):
                        if i < len(row): row[i] = transform_amount(row[i])
                elif section == "Cash Report":
                    for i in range(4, 7):
                        if i < len(row): row[i] = transform_amount(row[i])
                elif section == "Open Positions":
                    # Cost Price (8), Cost Basis (9), Close Price (10), Value (11), Unrealized P/L (12)
                    for i in range(8, 13):
                        if i < len(row): row[i] = transform_amount(row[i])
                elif section == "Trades":
                    # T. Price (8), C. Price (9), Proceeds (10), Comm/Fee (11), Basis (12), Realized P/L (13), MTM P/L (14)
                    for i in range(8, 15):
                        if i < len(row): row[i] = transform_amount(row[i])
                elif section == "Deposits & Withdrawals":
                    if len(row) > 5: row[5] = transform_amount(row[5])
                elif section in ["Dividends", "Withholding Tax"]:
                    if len(row) > 5: row[5] = transform_amount(row[5])
                elif section == "Change in Dividend Accruals":
                    # Tax (9), Fee (10), Gross Amount (12), Net Amount (13)
                    for i in [9, 10, 12, 13]:
                        if i < len(row): row[i] = transform_amount(row[i])

            writer.writerow(row)

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: python anonymize_ibkr.py <input_csv>")
        sys.exit(1)
    
    input_file = sys.argv[1]
    output_file = input_file.replace(".csv", "-anonymized.csv")
    if output_file == input_file:
        output_file = "anonymized_report.csv"
        
    anonymize(input_file, output_file)
    print(f"Anonymized file saved to: {output_file}")
