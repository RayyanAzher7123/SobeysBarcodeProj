using System.Text.RegularExpressions;

namespace Sobeys.Barcode
{
    public enum BarcodeType { Unknown, UPCA, UPCE, EAN13, EAN8 }

    // New: policy to resolve 8-digit ambiguity (could be EAN-8 or UPC-E)
    public enum EightDigitPolicy
    {
        PreferUpce,
        PreferEan8
    }

    public static class BarcodeUtils
    {
        private static readonly Regex DigitsOnly = new(@"^\d+$", RegexOptions.Compiled);

        // Default Identify uses PreferUpce (change to PreferEan8 if you want the other default)
        public static BarcodeType Identify(string input)
            => Identify(input, EightDigitPolicy.PreferUpce);

        // New overload: caller can pick the 8-digit policy explicitly
        public static BarcodeType Identify(string input, EightDigitPolicy policy)
        {
            var s = Normalize(input);
            if (!DigitsOnly.IsMatch(s)) return BarcodeType.Unknown;

            switch (s.Length)
            {
                case 6:
                    // 6-digit UPC-E core (no NSC/check)
                    return BarcodeType.UPCE;

                case 7:
                    // 7 digits: treat as EAN-8 body (no check)
                    return BarcodeType.EAN8;

                case 8:
                    {
                        if (policy == EightDigitPolicy.PreferUpce)
                        {
                            // Try UPC-E first (NSC must be 0/1 and expansion must succeed)
                            int nsc = s[0] - '0';
                            if ((nsc == 0 || nsc == 1) && TryExpandUPCE(s, assumeNumberSystem: nsc, out _))
                                return BarcodeType.UPCE;

                            // Then EAN-8 checksum
                            if (IsValidEAN8(s)) return BarcodeType.EAN8;

                            // Default: EAN-8
                            return BarcodeType.EAN8;
                        }
                        else // PreferEan8
                        {
                            // Try EAN-8 checksum first
                            if (IsValidEAN8(s)) return BarcodeType.EAN8;

                            // Then UPC-E expansion (NSC must be 0/1)
                            int nsc = s[0] - '0';
                            if ((nsc == 0 || nsc == 1) && TryExpandUPCE(s, assumeNumberSystem: nsc, out _))
                                return BarcodeType.UPCE;

                            // Default: EAN-8
                            return BarcodeType.EAN8;
                        }
                    }

                case 11:
                    // 11-digit UPC-A body (no check)
                    return BarcodeType.UPCA;

                case 12:
                    // 12-digit could be UPC-A complete OR EAN-13 body.
                    // We still call it UPCA and let AddCheckDigit decide.
                    return BarcodeType.UPCA;

                case 13:
                    return BarcodeType.EAN13;

                default:
                    return BarcodeType.Unknown;
            }
        }

        public static bool HasCheckDigit(string input)
        {
            var s = Normalize(input);
            var t = Identify(s);
            return t switch
            {
                BarcodeType.UPCA => s.Length == 12, // complete UPC-A
                BarcodeType.EAN13 => s.Length == 13, // complete EAN-13
                BarcodeType.EAN8 => s.Length == 8,  // complete EAN-8
                BarcodeType.UPCE => s.Length == 8,  // complete UPC-E (NSC+core+check)
                _ => false
            };
        }

        public static bool ValidateCheckDigit(string input)
        {
            var s = Normalize(input);
            var t = Identify(s);
            if (t == BarcodeType.Unknown || !DigitsOnly.IsMatch(s) || s.Length < 2) return false;

            if (t == BarcodeType.UPCE)
            {
                if (!TryExpandUPCE(s, assumeNumberSystem: 0, out var upca)) return false;
                return ValidateCheckDigit(upca);
            }

            var body = s[..^1];
            var given = s[^1] - '0';
            var calc = CalculateCheckDigit(body);
            return calc == given;
        }

        public static string AddCheckDigit(string input)
        {
            var s = Normalize(input);
            if (!DigitsOnly.IsMatch(s)) return s;

            // Length-driven resolution so it still works in ambiguous cases
            switch (s.Length)
            {
                case 6:
                    // UPC-E core (no NSC/check). Append UPC-E check using NSC=0 by default.
                    return AppendUPCECheckDigit(s, numberSystem: 0);

                case 7:
                    // EAN-8 body. Append EAN-8 check.
                    return s + CalculateCheckDigit(s);

                case 11:
                    // UPC-A body. Append UPC-A check.
                    return s + CalculateCheckDigit(s);

                case 12:
                    // If valid UPC-A (already has check), keep; else treat as EAN-13 body and append.
                    if (ValidateCheckDigit(s)) return s;
                    return s + CalculateCheckDigit(s);

                default:
                    // Other lengths (8, 13, etc.): assume check is present or not applicable.
                    return s;
            }
        }

        public static bool TryCompressUPCAtoUPCE(string upca, out string upce8)
        {
            upce8 = string.Empty;
            var s = Normalize(upca);
            if (Identify(s) != BarcodeType.UPCA || !ValidateCheckDigit(s)) return false;

            int n = s[0] - '0';
            if (n is not (0 or 1)) return false; // UPC-E only defined for NSC 0/1

            var M1 = s[1]; var M2 = s[2]; var M3 = s[3]; var M4 = s[4]; var M5 = s[5];
            var P1 = s[6]; var P2 = s[7]; var P3 = s[8]; var P4 = s[9]; var P5 = s[10];

            // Case product 0000 and P5 in 5..9
            if (P1 == '0' && P2 == '0' && P3 == '0' && P4 == '0' && (P5 is >= '5' and <= '9'))
                return BuildUPCE8(new string(new[] { M1, M2, M3, M4, M5, P5 }), n, out upce8);

            // Case M4==0 and product 000D5 -> D6=4
            if (M4 == '0' && P1 == '0' && P2 == '0' && P3 == '0')
                return BuildUPCE8(new string(new[] { M1, M2, M3, M5, P5, '4' }), n, out upce8);

            // Case M4==0, M5==0 and product 000D4D5 -> D6=3
            if (M4 == '0' && M5 == '0' && P1 == '0' && P2 == '0' && P3 == '0')
                return BuildUPCE8(new string(new[] { M1, M2, M3, P4, P5, '3' }), n, out upce8);

            // Case M4 in 0..2, M5==0 and product 000D4D5 -> D6=M4
            if (M5 == '0' && P1 == '0' && P2 == '0' && P3 == '0' && (M4 is '0' or '1' or '2'))
                return BuildUPCE8(new string(new[] { M1, M2, M3, P4, P5, M4 }), n, out upce8);

            return false; // Not zero-compressible per GS1 rules
        }

        public static bool TryExpandUPCE(string upceInput, int assumeNumberSystem, out string upca12)
        {
            upca12 = string.Empty;
            var s = Normalize(upceInput);
            if (!DigitsOnly.IsMatch(s)) return false;

            string core6;
            int n;
            int givenCheck = -1;

            if (s.Length == 6) { core6 = s; n = (assumeNumberSystem is 0 or 1) ? assumeNumberSystem : 0; }
            else if (s.Length == 7) { n = s[0] - '0'; if (n is not (0 or 1)) return false; core6 = s[1..]; }
            else if (s.Length == 8) { n = s[0] - '0'; if (n is not (0 or 1)) return false; givenCheck = s[^1] - '0'; core6 = s.Substring(1, 6); }
            else return false;

            var d1 = core6[0]; var d2 = core6[1]; var d3 = core6[2];
            var d4 = core6[3]; var d5 = core6[4]; var d6 = core6[5];

            string manufacturer, product;
            if (d6 is >= '5' and <= '9') { manufacturer = $"{d1}{d2}{d3}{d4}{d5}"; product = $"0000{d6}"; }
            else if (d6 == '4') { manufacturer = $"{d1}{d2}{d3}0{d4}"; product = $"0000{d5}"; }
            else if (d6 == '3') { manufacturer = $"{d1}{d2}{d3}00"; product = $"000{d4}{d5}"; }
            else { manufacturer = $"{d1}{d2}{d3}{d6}0"; product = $"000{d4}{d5}"; }

            var body11 = $"{n}{manufacturer}{product}";
            var check = CalculateCheckDigit(body11);
            var upca = body11 + check;

            if (givenCheck >= 0 && givenCheck != check) return false;
            upca12 = upca;
            return true;
        }

        public static bool TryConvert(string input, out string converted)
        {
            converted = string.Empty;
            var t = Identify(input);
            var s = Normalize(input);

            if (t == BarcodeType.UPCA && TryCompressUPCAtoUPCE(s, out var upce8))
            {
                converted = upce8;
                return true;
            }
            if (t == BarcodeType.UPCE && TryExpandUPCE(s, 0, out var upca12))
            {
                converted = upca12;
                return true;
            }
            return false;
        }

        public static int CalculateCheckDigit(string body)
        {
            int sum = 0; bool triple = true;
            for (int i = body.Length - 1; i >= 0; i--)
            {
                int d = body[i] - '0';
                sum += d * (triple ? 3 : 1);
                triple = !triple;
            }
            int mod = sum % 10;
            return mod == 0 ? 0 : 10 - mod;
        }

        private static string Normalize(string s) => (s ?? string.Empty).Trim().Replace(" ", "");

        private static bool BuildUPCE8(string core6, int numberSystem, out string upce8)
        {
            upce8 = string.Empty;
            if (!TryExpandUPCE(core6, numberSystem, out var upca)) return false;
            var check = upca[^1];
            upce8 = $"{numberSystem}{core6}{check}";
            return true;
        }

        private static string AppendUPCECheckDigit(string upce6, int numberSystem)
        {
            if (!TryExpandUPCE(upce6, numberSystem, out var upca))
                throw new InvalidOperationException("Invalid UPC-E for check digit calculation.");
            var check = upca[^1];
            return $"{numberSystem}{upce6}{check}";
        }

        private static bool IsValidEAN8(string s)
        {
            // s is assumed 8 digits
            if (s.Length != 8 || !DigitsOnly.IsMatch(s)) return false;
            var body = s.Substring(0, 7);
            int given = s[7] - '0';
            int calc = CalculateCheckDigit(body);
            return calc == given;
        }
    }
}
