Sobeys Barcode Suite: This project is a .NET-based utility library for working with retail barcodes such as UPC-A, UPC-E, EAN-13, and EAN-8.

What it does:
1)Identify the type of barcode based on length and format rules.

2)Validate barcodes by checking their control digit (checksum).

3)Add check digits automatically to partial codes (e.g., 11-digit UPC-A or 12-digit EAN-13 bodies).

4)Convert between UPC-A and UPC-E (compress or expand).

5)Handle EAN-8 codes with proper check digit validation.

How it works:

i)Input: A numeric string (digits only).

ii)The program:

1)Detects if it’s UPC-A, UPC-E, EAN-13, or EAN-8.

2)Checks if the code already has a valid check digit.

3)If not, it can generate the correct check digit and append it.

4)If UPC-A can be compressed into UPC-E, or UPC-E can be expanded back, it performs that conversion.

iii)Output: A validated or transformed barcode string along with its type.
