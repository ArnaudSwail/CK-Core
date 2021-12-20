using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CK.Core
{
    /// <summary>
    /// Supports basic operations for "Match and Forward" pattern at the <see cref="ReadOnlySpan{T}"/> level.
    /// This doesn't offer expectation support. For simple patterns this may be enough however to be able to
    /// have a detailed reason of a match failure, the <see cref="ROSpanCharMatcher"/> should be used.  
    /// </summary>
    public static class ReadOnlySpanCharExtensions
    {
        /// <summary>
        /// Forwards <paramref name="head"/> by <paramref name="length"/> even if actual head's length is shorter and
        /// returns the count of remaining characters (the new head's length).
        /// </summary>
        /// <param name="head">This head.</param>
        /// <param name="length">The length. Must be 0 or positive otherwise an ArgumentOutOfRangeException is thrown.</param>
        /// <returns>The remainder's head length.</returns>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static int SafeForward( this ref ReadOnlySpan<char> head, int length )
        {
            // Slice throws ArgumentOutOfRangeException if length is negative.
            head = head.Slice( Math.Min( length, head.Length ) );
            return head.Length;
        }

        /// <summary>
        /// Tries to match a specific string.
        /// </summary>
        /// <param name="head">This head.</param>
        /// <param name="value">The string value to match.</param>
        /// <param name="comparison">How to compare.</param>
        /// <returns>True on success, false otherwise.</returns>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool TryMatch( this ref ReadOnlySpan<char> head, ReadOnlySpan<char> value, StringComparison comparison = StringComparison.Ordinal )
        {
            if( head.StartsWith( value, comparison ) )
            {
                head = head.Slice( value.Length );
                return true;
            }
            return false;
        }

        /// <summary>
        /// Tries to match a character.
        /// </summary>
        /// <param name="head">This head.</param>
        /// <param name="value">The character to match.</param>
        /// <param name="comparison">How to compare.</param>
        /// <returns>True on success, false otherwise.</returns>
        [MethodImpl( MethodImplOptions.AggressiveInlining )]
        public static bool TryMatch( this ref ReadOnlySpan<char> head, char value, StringComparison comparison = StringComparison.Ordinal )
        {
            if( head.StartsWith( MemoryMarshal.CreateReadOnlySpan( ref value, 1 ), comparison ) )
            {
                head = head.Slice( 1 );
                return true;
            }
            return false;
        }

        /// <summary>
        /// Tries to skip a sequence of white spaces.
        /// Use <paramref name="minCount"/> = 0 to skip any number of white spaces.
        /// </summary>
        /// <param name="head">The head.</param>
        /// <param name="minCount">Minimal number of white spaces to skip.</param>
        /// <returns>True on success, false if <paramref name="minCount"/> white spaces cannot be skipped before the end of the head.</returns>
        public static bool TrySkipWhiteSpaces( this ref ReadOnlySpan<char> head, int minCount = 1 )
        {
            int i = 0;
            int len = head.Length;
            while( len != 0 && char.IsWhiteSpace( head[i] ) ) { ++i; --len; }
            if( i >= minCount )
            {
                head = head.Slice( i );
                return true;
            }
            return false;
        }

        /// <summary>
        /// Tries to skip a sequence of decimal digits (0-9).
        /// Use <paramref name="minCount"/> = 0 to skip any number of decimal digits.
        /// </summary>
        /// <param name="head">The head.</param>
        /// <param name="minCount">Minimal number of decimal digits to skip.</param>
        /// <returns>True on success, false if <paramref name="minCount"/> decimal digits cannot be skipped before the end of the head.</returns>
        public static bool TrySkipDigits( this ref ReadOnlySpan<char> head, int minCount = 1 )
        {
            int i = 0;
            int len = head.Length;
            char c;
            while( len != 0 && (c = head[i]) >= '0' && c <= '9' ) { ++i; --len; }
            if( i >= minCount )
            {
                head = head.Slice( i );
                return true;
            }
            return false;
        }

        /// <summary>
        /// Tries to parse a Guid.
        /// </summary>
        /// <remarks>
        /// Any of the 5 forms of Guid can be matched:
        /// <list type="table">
        /// <item><term>N</term><description>00000000000000000000000000000000</description></item>
        /// <item><term>D</term><description>00000000-0000-0000-0000-000000000000</description></item>
        /// <item><term>B</term><description>{00000000-0000-0000-0000-000000000000}</description></item>
        /// <item><term>P</term><description>(00000000-0000-0000-0000-000000000000)</description></item>
        /// <item><term>X</term><description>{0x00000000,0x0000,0x0000,{0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00}}</description></item>
        /// </list>
        /// </remarks>
        /// <param name="head">This head.</param>
        /// <param name="id">The result Guid. <see cref="Guid.Empty"/> on failure.</param>
        /// <returns>True on success, false otherwise.</returns>
        public static bool TryMatchGuid( this ref ReadOnlySpan<char> head, out Guid id )
        {
            id = Guid.Empty;
            if( head.Length < 32 ) return false;
            if( head[0] == '{' )
            {
                // Form "B" or "X".
                if( head.Length < 38 ) return false;
                if( head[37] == '}' )
                {
                    // The "B" form.
                    if( Guid.TryParseExact( head.Slice( 0, 38 ), "B", out id ) )
                    {
                        head = head.Slice( 38 );
                        return true;
                    }
                    return false;
                }
                // The "X" form.
                if( head.Length >= 68 && Guid.TryParseExact( head.Slice( 0, 68 ), "X", out id ) )
                {
                    head = head.Slice( 68 );
                    return true;
                }
                return false;
            }
            if( head[0] == '(' )
            {
                // Can only be the "P" form.
                if( head.Length >= 38 && Guid.TryParseExact( head.Slice( 0, 38 ), "P", out id ) )
                {
                    head = head.Slice( 38 );
                    return true;
                }
                return false;
            }
            if( head[0].HexDigitValue() >= 0 )
            {
                // The "N" or "D" form.
                if( head.Length >= 36 && head[8] == '-' )
                {
                    // The ""D" form.
                    if( Guid.TryParseExact( head.Slice( 0, 36 ), "D", out id ) )
                    {
                        head = head.Slice( 36 );
                        return true;
                    }
                    return false;
                }
                if( Guid.TryParseExact( head.Slice( 0, 32 ), "N", out id ) )
                {
                    head = head.Slice( 32 );
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Tries to parse a boolean "true" or "false" (case insensitive).
        /// </summary>
        /// <param name="head">This head.</param>
        /// <param name="b">The result boolean. False on failure.</param>
        /// <returns>True on success, false otherwise.</returns>
        public static bool TryMatchBool( this ref ReadOnlySpan<char> head, out bool b )
        {
            b = false;
            if( head.Length >= 4 )
            {
                if( head.TryMatch( "false", StringComparison.OrdinalIgnoreCase )
                    || (b = head.TryMatch( "true", StringComparison.OrdinalIgnoreCase )) )
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Tries to parse an hexadecimal values: between 1 and 16 0-9, A-F or a-f digits.
        /// </summary>
        /// <param name="head">This head.</param>
        /// <param name="value">Resulting value on success.</param>
        /// <param name="minDigit">Minimal digit count. Must be between 1 and 16 and smaller or equal to <paramref name="maxDigit"/>.</param>
        /// <param name="maxDigit">Maximal digit count. Must be between 1 and 16.</param>
        /// <returns>True on success, false otherwise.</returns>
        public static bool TryMatchHexNumber( this ref ReadOnlySpan<char> head, out ulong value, int minDigit = 1, int maxDigit = 16 )
        {
            Throw.CheckArgument( minDigit > 0 );
            Throw.CheckArgument( maxDigit <= 16 );
            Throw.CheckArgument( minDigit <= maxDigit );
            value = 0;
            if( !head.IsEmpty )
            {
                int idx = 0;
                int len = head.Length;
                while( --maxDigit >= 0 && --len >= 0 )
                {
                    int cN = head[idx].HexDigitValue();
                    if( cN < 0 || cN > 15 ) break;
                    ++idx;
                    value <<= 4;
                    value |= (uint)cN;
                }
                if( idx >= minDigit )
                {
                    head = head.Slice( idx );
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Tries to match an Int32 value. A signed integer starts with a '-' and must not be followed by white spaces.
        /// If the value is too big for an Int32, it fails.
        /// <para>
        /// When <paramref name="allowLeadingZeros"/> is false, the value must not start with '0' ('0' is valid but '0d', where d is any digit, is not)
        /// and '-0' is valid but '-0d' (where d is any digit) is not.
        /// </para>
        /// </summary>
        /// <param name="head">This head.</param>
        /// <param name="i">The result integer. 0 on failure.</param>
        /// <param name="allowLeadingZeros">False to forbid leading zeros.</param>
        /// <param name="minValue">Optional minimal value.</param>
        /// <param name="maxValue">Optional maximal value.</param>
        /// <returns>True on success, false otherwise.</returns>
        public static bool TryMatchInt32( this ref ReadOnlySpan<char> head, out int i, bool allowLeadingZeros = true, int minValue = int.MinValue, int maxValue = int.MaxValue )
        {
            i = 0;
            long value = 0;
            bool signed;
            if( head.IsEmpty ) return false;
            ReadOnlySpan<char> h = head;
            if( (signed = h[0] == '-') )
            {
                h = h.Slice( 1 );
                if( h.IsEmpty || minValue >= 0 ) return false;
            }
            char c;
            while( h[0] == '0' )
            {
                h = h.Slice( 1 );
                if( h.Length > 0 && (c = h[0]) >= '0' && c <= '9' )
                {
                    if( allowLeadingZeros )
                    {
                        if( c == '0' ) continue;
                        break;
                    }
                    return false;
                }
                head = h;
                return true;
            }
            int idx = 0;
            unchecked
            {
                long iMax = Int32.MaxValue;
                if( signed ) iMax = iMax + 1;
                for( ; idx < h.Length; idx++ )
                {
                    if( (c = h[idx]) < '0' || c > '9' ) break;
                    value = value * 10 + (c - '0');
                    if( value > iMax ) break;
                }
            }
            if( idx > 0 )
            {
                if( signed ) value = -value;
                if( value >= minValue && value <= maxValue )
                {
                    i = (int)value;
                    head = h.Slice( idx );
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Tries to skip a double value, using the regular expression "-?[0-9]+(\.[0-9]+)?((e|E)(\+|-)?[0-9]+)?".
        /// </summary>
        /// <param name="head">This head.</param>
        /// <returns>True on success, false otherwise.</returns>
        public static bool TrySkipDouble( this ref ReadOnlySpan<char> head )
        {
            if( head.Length == 0 ) return false;
            var h = head;
            if( h[0] == '-' ) h = h.Slice( 1 );
            if( !h.TrySkipDigits( 1 ) ) return false;
            if( h[0] == '.' )
            {
                h = h.Slice( 1 );
                if( !h.TrySkipDigits( 1 ) ) return false;
            }
            if( h[0] == 'e' || h[0] == 'E' )
            {
                h = h.Slice( h[1] == '-' || h[1] == '+' ? 2 : 1 );
                if( !h.TrySkipDigits( 1 ) ) return false;
            }
            head = head.Slice( head.Length - h.Length );
            return true;
        }

        /// <summary>
        /// Tries to match a double value.
        /// A first shallow parse is unfortunately required (calling <see cref="TrySkipDouble(ref ReadOnlySpan{char})"/>) since
        /// parsing double is a fairly complex task and the standard <see cref="Double.TryParse(ReadOnlySpan{char}, out double)"/>
        /// (like all other TryParse) doesn't give us the actual parsed length: we have to figure it out first.
        /// </summary>
        /// <param name="head">This head.</param>
        /// <param name="value">The result double. 0.0 on failure.</param>
        /// <returns></returns>
        public static bool TryMatchDouble( this ref ReadOnlySpan<char> head, out double value )
        {
            value = 0;
            var h = head;
            if( !TrySkipDouble( ref h ) ) return false;
            int len = head.Length - h.Length;
            if( !double.TryParse( head.Slice( 0, len ), NumberStyles.Float, CultureInfo.InvariantCulture, out value ) )
            {
                return false;
            }
            head = head.Slice( len );
            return true;
        }
    }
}
