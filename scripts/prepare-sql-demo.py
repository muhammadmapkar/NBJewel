#!/usr/bin/env python3
# Role: Generates a non-destructive, reviewable SQL setup script offline. Never connects to SQL.
import re
from pathlib import Path
ROOT = Path(__file__).resolve().parent.parent
SOURCES = ['item_catalog_schema.sql', 'identity-schema.sql', 'customer-portal-schema.sql', 'platform-schema.sql', 'item-image-assets-schema.sql', 'storefront-locations-schema.sql']

def quote(value):
    return "N'" + value.replace("'", "''") + "'"

def prepare():
    output = ["-- Role: Generated additive SQL demo schema for one owner-selected database. Review before running.",
              "-- Run with sqlcmd -b. Does not create/drop databases, seed data, or replace existing tables.",
              "SET XACT_ABORT ON;", "BEGIN TRY", "BEGIN TRANSACTION;"]
    # Only known additive upgrades. An incompatible existing schema fails rather than being rebuilt.
    for column, definition in [('StorefrontJson','nvarchar(max) NULL CHECK (StorefrontJson IS NULL OR ISJSON(StorefrontJson)=1)'), ('StoneCharge','decimal(12,2) NOT NULL DEFAULT 0 CHECK (StoneCharge >= 0)'), ('VatPercent','decimal(5,2) NOT NULL DEFAULT 0 CHECK (VatPercent BETWEEN 0 AND 100)')]:
        output.append(f"IF OBJECT_ID(N'dbo.Item', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.Item', N'{column}') IS NULL EXEC({quote('ALTER TABLE dbo.Item ADD '+column+' '+definition+';')});")
    output.append("IF OBJECT_ID(N'dbo.AiPublicationProducts', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.AiPublicationProducts', N'AssetRevisionId') IS NULL EXEC(N'ALTER TABLE dbo.AiPublicationProducts ADD AssetRevisionId nvarchar(32) NULL;');")
    for source in SOURCES:
        text = (ROOT/'docs'/source).read_text()
        text = re.sub(r'/\*.*?\*/', '', text, flags=re.S)
        text = re.sub(r'--[^\n]*', '', text)
        text = '\n'.join(line.rstrip() for line in text.splitlines())
        for match in re.finditer(r'CREATE TABLE\s+([\w.\[\]]+)\s*\((.*?)\);', text, re.S|re.I):
            table = match[1].replace('[','').replace(']','')
            if '.' not in table: table = 'dbo.'+table
            output.append(f"IF OBJECT_ID({quote(table)}, N'U') IS NULL EXEC({quote(match[0])});")
            for column in re.findall(r'^\s*\[?(\w+)\]?\s+(?:int|bigint|bit|nvarchar|decimal|datetime2|datetimeoffset|date|varchar|float)\b', match[2], re.M|re.I):
                output.append(f"IF COL_LENGTH({quote(table)}, {quote(column)}) IS NULL THROW 51000, 'Incompatible schema: {table}.{column} missing', 1;")
            # Existing columns must match the reviewed contract, not merely have the same names.
            for field in re.finditer(r'^\s*\[?(\w+)\]?\s+(int|bigint|bit|nvarchar|decimal|datetime2|datetimeoffset|date|varchar|float)\b(?:\(([^)]+)\))?', match[2], re.M|re.I):
                column, kind, size = field[1], field[2].lower(), field[3]
                incompatible = f"TYPE_NAME(system_type_id)<>{quote(kind)}"
                if kind in ('nvarchar','varchar') and size:
                    length = -1 if size.lower()=='max' else int(size)*(2 if kind=='nvarchar' else 1)
                    incompatible += f" OR max_length<>{length}"
                if kind=='decimal' and size:
                    precision, scale = map(int,size.split(','))
                    incompatible += f" OR precision<>{precision} OR scale<>{scale}"
                output.append(f"IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID({quote(table)}) AND name={quote(column)} AND ({incompatible})) THROW 51000, 'Incompatible type: {table}.{column}', 1;")
        for match in re.finditer(r'CREATE (?:UNIQUE )?INDEX\s+([\w\[\]]+)\s+ON\s+([\w.\[\]]+)\s*.*?;', text, re.S|re.I):
            index=match[1].strip('[]');table=match[2].replace('[','').replace(']','')
            if '.' not in table: table='dbo.'+table
            output.append(f"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID({quote(table)}) AND name={quote(index)}) EXEC({quote(match[0])});")
    output += ["IF COLUMNPROPERTY(OBJECT_ID(N'dbo.AiPublicationProducts'), N'ItemId', 'IsIdentity') = 1 THROW 51000, 'Publication ItemId must be externally assigned; review legacy identity migration separately.', 1;",
               "COMMIT TRANSACTION;", "END TRY", "BEGIN CATCH", "IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;", "THROW;", "END CATCH;"]
    return '\n'.join(output)+'\n'
if __name__ == '__main__':
    (ROOT/'docs'/'sql-demo-schema.sql').write_text(prepare())
    print('Prepared docs/sql-demo-schema.sql offline. No database connection made.')
