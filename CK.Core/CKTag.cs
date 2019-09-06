using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CK.Core;
using System.Threading;
using System.ComponentModel;

namespace CK.Core
{
    /// <summary>
    /// A tag is an immutable object (thread-safe), associated to a unique string inside a <see cref="Context"/>, that can be atomic ("Alt", "Home", "Ctrl") or 
    /// combined ("Alt|Ctrl", "Alt|Ctrl|Home"). The only way to obtain a CKTag is to call <see cref="CKTagContext.FindOrCreate(string)"/> (from 
    /// a string) or to use one of the available combination methods (<see cref="Union"/>, <see cref="Except"/>, <see cref="SymmetricExcept"/> or <see cref="Intersect"/> ).
    /// </summary>
    /// <remarks>
    /// A CKTag is easily serializable as its <see cref="ToString"/> representation and restored with <see cref="CKTagContext.FindOrCreate(string)"/>
    /// on the appropriate context.
    /// </remarks>
    public sealed class CKTag : IComparable<CKTag>, IEquatable<CKTag>
    {
        readonly CKTagContext _context;
        readonly string _tag;
        readonly IReadOnlyList<CKTag> _tags;

        /// <summary>
        /// Initializes the new empty tag of a CKTagContext.
        /// </summary>
        internal CKTag( CKTagContext ctx )
        {
            Debug.Assert( ctx.EmptyTag == null, "There is only one empty tag per context." );
            _context = ctx;
            _tag = String.Empty;
            _tags = Util.Array.Empty<CKTag>();
        }

        /// <summary>
        /// Initializes a new atomic tag.
        /// </summary>
        internal CKTag( CKTagContext ctx, string atomicTag )
        {
            Debug.Assert( atomicTag.Contains( ctx.Separator ) == false );
            _context = ctx;
            _tag = atomicTag;
            _tags = new CKTag[] { this };
        }

        /// <summary>
        /// Initializes a new combined tag.
        /// </summary>
        internal CKTag( CKTagContext ctx, string combinedTag, IReadOnlyList<CKTag> tags )
        {
            Debug.Assert( combinedTag.IndexOf( ctx.Separator ) > 0 && tags.Count > 1, "There is more than one tag in a Combined Tag." );
            Debug.Assert( tags.All( m => m.IsAtomic ), "Provided tags are all atomic." );
            Debug.Assert( tags.GroupBy( m => m ).Where( g => g.Count() != 1 ).Count() == 0, "No duplicate in atomic in tags." );
            _context = ctx;
            _tag = combinedTag;
            _tags = tags;
        }

        /// <summary>
        /// Gets the <see cref="CKTagContext"/> to which this tag belongs. 
        /// </summary>
        public CKTagContext Context => _context;

        /// <summary>
        /// Gets the multi tags in an ordered manner separated by +.
        /// </summary>
        /// <returns>This multi tags as a string.</returns>
        public override string ToString() => _tag;

        /// <summary>
        /// Gets the atomic tags that this tag contains.
        /// This list does not contain the empty tag and is sorted according to the name of the atomic tags (lexical order): this is the 
        /// same as the <see cref="ToString"/> representation.
        /// Note that it is in reverse order regarding <see cref="CompareTo"/> ("A" that is stronger than "B" appears before "B").
        /// </summary>
        public IReadOnlyList<CKTag> AtomicTags => _tags; 

        /// <summary>
        /// Gets a boolean indicating whether this tag is the empty tag (<see cref="AtomicTags"/> is empty
        /// and <see cref="Fallbacks"/> contains only itself).
        /// </summary>
        public bool IsEmpty => _tag.Length == 0; 

        /// <summary>
        /// Gets a boolean indicating whether this tag contains zero 
        /// (the empty tag is considered as an atomic tag) or only one atomic tag.
        /// </summary>
        /// <remarks>
        /// For atomic tags (and the empty tag itself), <see cref="Fallbacks"/> contains only the <see cref="CKTagContext.EmptyTag"/>.
        /// </remarks>
        public bool IsAtomic => _tags.Count <= 1; 

        /// <summary>
        /// Compares this tag with another one.
        /// The <see cref="Context"/> is the primary key (see <see cref="CKTagContext.CompareTo"/>), then comes 
        /// the number of tags (more tags is greater) and then comes the string representation of the tag in 
        /// reverse lexical order (<see cref="StringComparer.Ordinal"/>): "A" is greater than "B".
        /// </summary>
        /// <param name="other">The tag to compare to.</param>
        /// <returns>A negative, zero or positive value.</returns>
        public int CompareTo( CKTag other )
        {
            if( other == null ) throw new ArgumentNullException( "other" );
            if( ReferenceEquals( this, other ) ) return 0;
            int cmp = _context.CompareTo( other._context );
            if( cmp == 0 )
            {
                cmp = _tags.Count - other.AtomicTags.Count;
                if( cmp == 0 ) cmp = StringComparer.Ordinal.Compare( other._tag, _tag );
            }
            return cmp;
        }

        /// <summary>
        /// Checks equality of this tag with another one.
        /// </summary>
        /// <param name="other">The tag to compare to.</param>
        /// <returns>True on equality.</returns>
        public bool Equals( CKTag other )
        {
            return ReferenceEquals( this, other );
        }

        /// <summary>
        /// Checks if each and every atomic tags of <paramref name="other" /> exists in this tag.
        /// </summary>
        /// <param name="other">The tag(s) to find.</param>
        /// <returns>True if all the specified tags appear in this tag.</returns>
        /// <remarks>
        /// Note that <see cref="CKTagContext.EmptyTag"/> is contained (in the sense of this IsSupersetOf method) by definition in any tag 
        /// (including itself): this is the opposite of the <see cref="Overlaps"/> method.
        /// </remarks>
        public bool IsSupersetOf( CKTag other )
        {
            if( other == null ) throw new ArgumentNullException( "other" );
            if( other.Context != _context ) throw new InvalidOperationException( Impl.CoreResources.TagsMustBelongToTheSameContext );
            if( _tags.Count < other._tags.Count ) return false;
            bool foundAlien = false;
            Process( this, other,
                     null,
                     delegate( CKTag m ) { foundAlien = true; return false; },
                     null );
            return !foundAlien;
        }

        /// <summary>
        /// Checks if one of the atomic tags of <paramref name="other" /> exists in this tag.
        /// </summary>
        /// <param name="other">The tag to find.</param>
        /// <returns>Returns true if one of the specified tags appears in this tag.</returns>
        /// <remarks>
        /// When true, this ensures that <see cref="Intersect"/>( <paramref name="other"/> ) != <see cref="CKTagContext.EmptyTag"/>. 
        /// The empty tag is not contained (in the sense of this ContainsOne method) in any tag (including itself). This is the opposite
        /// of the <see cref="IsSupersetOf"/> method.
        /// </remarks>
        public bool Overlaps( CKTag other )
        {
            if( other == null ) throw new ArgumentNullException( "other" );
            if( other.Context != _context ) throw new InvalidOperationException( Impl.CoreResources.TagsMustBelongToTheSameContext );
            bool found = false;
            Process( this, other,
                null,
                null,
                delegate( CKTag m ) { found = true; return false; } );
            return found;
        }

        class ListTag : List<CKTag>
        {
            public bool TrueAdd( CKTag t )
            {
                Add( t );
                return true;
            }
        }

        /// <summary>
        /// Obtains a <see cref="CKTag"/> that contains the atomic tags from both this tag and <paramref name="other"/>.
        /// </summary>
        /// <param name="other">Tag that must be kept.</param>
        /// <returns>The resulting tag.</returns>
        public CKTag Intersect( CKTag other )
        {
            if( ReferenceEquals( other, this ) ) return this;
            if( other == null ) throw new ArgumentNullException( "other" );
            if( other.Context != _context ) throw new InvalidOperationException( Impl.CoreResources.TagsMustBelongToTheSameContext );
            ListTag m = new ListTag();
            Process( this, other, null, null, m.TrueAdd );
            return _context.FindOrCreate( m );
        }

        /// <summary>
        /// Obtains a <see cref="CKTag"/> that combines this one and 
        /// the tzg(s) specified by the parameter. 
        /// </summary>
        /// <param name="other">Tag to add.</param>
        /// <returns>The resulting tag.</returns>
        public CKTag Union( CKTag other )
        {
            if( ReferenceEquals( other, this ) ) return this;
            if( other == null ) throw new ArgumentNullException( nameof( other ) );
            if( other.Context != _context ) throw new InvalidOperationException( Impl.CoreResources.TagsMustBelongToTheSameContext );
            ListTag m = new ListTag();
            Func<CKTag,bool> add = m.TrueAdd;
            Process( this, other, add, add, add );
            return _context.FindOrCreate( m );
        }

        /// <summary>
        /// Obtains a <see cref="CKTag"/> from which tag(s) specified by the parameter are removed.
        /// </summary>
        /// <param name="other">Tag to remove.</param>
        /// <returns>The resulting tag.</returns>
        public CKTag Except( CKTag other )
        {
            if( ReferenceEquals( other, this ) ) return _context.EmptyTag;
            if( other == null ) throw new ArgumentNullException( nameof( other ) );
            if( other.Context != _context ) throw new InvalidOperationException( Impl.CoreResources.TagsMustBelongToTheSameContext );
            ListTag m = new ListTag();
            Process( this, other, m.TrueAdd, null, null );
            return _context.FindOrCreate( m );
        }

        /// <summary>
        /// Obtains a <see cref="CKTag"/> where the atomic tags of <paramref name="other" /> are removed (resp. added) depending 
        /// on whether they exist (resp. do not exist) in this tag. This is like an Exclusive Or (XOR).
        /// </summary>
        /// <param name="other">Tag to toggle.</param>
        /// <returns>The resulting tag.</returns>
        public CKTag SymmetricExcept( CKTag other )
        {
            if( ReferenceEquals( other, this ) ) return _context.EmptyTag;
            if( other == null ) throw new ArgumentNullException( nameof( other ) );
            if( other.Context != _context ) throw new InvalidOperationException( Impl.CoreResources.TagsMustBelongToTheSameContext );
            ListTag m = new ListTag();
            Func<CKTag,bool> add = m.TrueAdd;
            Process( this, other, add, add, null );
            return _context.FindOrCreate( m );
        }

        /// <summary>
        /// Applies the given <see cref="SetOperation"/>.
        /// </summary>
        /// <param name="other">Tag to combine.</param>
        /// <param name="operation">Set operation.</param>
        /// <returns>Resulting tag.</returns>
        public CKTag Apply( CKTag other, SetOperation operation )
        {
            if( other == null ) throw new ArgumentNullException( nameof( other ) );
            switch( operation )
            {
                case SetOperation.Union: return Union( other );
                case SetOperation.Except: return Except( other );
                case SetOperation.Intersect: return Intersect( other );
                case SetOperation.SymetricExcept: return SymmetricExcept( other );
                case SetOperation.None: return this;
            }
            Debug.Assert( operation == SetOperation.Replace, "All operations are covered." );
            return other;
        }

        /// <summary>
        /// Common process function where 3 predicates drive the result: each atomic tag is submitted to one of the 3 predicates
        /// depending on whether it is only in the left, only in the right or appears in both tags.
        /// When returning false, a predicate stops the process.
        /// </summary>
        /// <remarks>
        /// When this predicate is 'adding the tag to a list', we can draw the following table where '1' means the predicate exists and '0' means
        /// no predicate (or the 'always true' one):
        /// 
        ///             0, 0, 0 =  -- 'Empty'
        /// Intersect   0, 0, 1 = Intersect (keep commons) => /Toggle
        ///             0, 1, 0 =  -- 'Cleanup' (keep theirs only) => /Remove 
        ///             0, 1, 1 =  -- 'Other' (keep theirs and commons, reject mine) => /This
        /// Except      1, 0, 0 = Remove (keep mine only) => /Cleanup
        ///             1, 0, 1 =  -- 'This' (keep mine and commons and reject theirs) => /Other
        /// Toggle      1, 1, 0 = Toggle (keep mine, theirs, but reject commons) => /Intersect
        /// Union       1, 1, 1 = Add
        /// 
        /// This shows that our 4 methods Intersect, Remove, Toggle and Add cover the interesting cases - others are either symetric or useless.
        /// </remarks>
        static void Process( CKTag left, CKTag right, Func<CKTag,bool> onLeft, Func<CKTag,bool> onRight, Func<CKTag,bool> onBoth )
        {
            IReadOnlyList<CKTag> l = left.AtomicTags;
            int cL = l.Count;
            int iL = 0;
            IReadOnlyList<CKTag> r = right.AtomicTags;
            int cR = r.Count;
            int iR = 0;
            for( ; ; )
            {
                if( cL == 0 )
                {
                    while( cR-- > 0 )
                    {
                        if( onRight == null || !onRight( r[iR++] ) ) break;
                    }
                    return;
                }
                if( cR == 0 )
                {
                    while( cL-- > 0 )
                    {
                        if( onLeft == null || !onLeft( l[iL++] ) ) break;
                    }
                    return;
                }
                Debug.Assert( iL >= 0 && iL < l.Count && iR >= 0 && iR < r.Count, "End of lists is handled above." );
                CKTag eL = l[iL];
                CKTag eR = r[iR];
                if( eL == eR )
                {
                    if( onBoth != null && !onBoth( eL ) ) break;
                    iL++;
                    cL--;
                    iR++;
                    cR--;
                }
                else
                {
                    int cmp = eL.CompareTo( eR );
                    Debug.Assert( cmp != 0, "Since they are not the same." );
                    if( cmp > 0 )
                    {
                        if( onLeft != null && !onLeft( eL ) ) break;
                        iL++;
                        cL--;
                    }
                    else
                    {
                        if( onRight != null && !onRight( eR ) ) break;
                        iR++;
                        cR--;
                    }
                }
            }
        }

        /// <summary>
        /// Gets the number of <see cref="Fallbacks"/>. It is 2^<see cref="AtomicTags"/>.<see cref="IReadOnlyCollection{T}.Count"/> - 1 since this
        /// tag itself does not appear in the fallbacks, but it is always 1 for atomic and the empty tag (the empty tag always ends the list).
        /// </summary>
        public int FallbacksCount => _tags.Count > 1 ? ( 1 << _tags.Count ) - 1 : 1;

        /// <summary>
        /// Gets the number of <see cref="Fallbacks"/>. It is 2^<see cref="AtomicTags"/>.<see cref="IReadOnlyCollection{T}.Count"/> - 1 since this
        /// tag itself does not appear in the fallbacks, but it is always 1 for atomic and the empty tag (the empty tag always ends the list).
        /// </summary>
        public long FallbacksLongCount => _tags.Count > 1 ? ( 1L << _tags.Count ) - 1L : 1L; 

        /// <summary>
        /// Gets an enumeration of fallbacks to consider for this tag ordered from best to worst.
        /// This tag does not start the list but the <see cref="CKTagContext.EmptyTag"/> always ends this list.
        /// </summary>
        /// <remarks>
        /// For atomic tags (and the empty tag itself), <see cref="Fallbacks"/> contains only the <see cref="CKTagContext.EmptyTag"/>.
        /// </remarks>
        public IEnumerable<CKTag> Fallbacks
        {
            get
            {
                if( _tags.Count <= 1 ) return _context.EnumWithEmpty;
                return ComputeFallbacks();
            }
        }

        IEnumerable<CKTag> ComputeFallbacks()
        {
            int _currentLength = _tags.Count - 1;
            Debug.Assert( _currentLength >= 1, "Empty and atomic tags are handled explicitly (EnumWithEmpty)." );
            if( _currentLength > 1 )
            {
                int nbTag = _tags.Count;
                bool[] kept = new bool[nbTag];
                CKTag[] v = new CKTag[_currentLength];
                do
                {
                    int i = nbTag;
                    while( --i >= _currentLength ) kept[i] = false;
                    int kMax = i;
                    while( i >= 0 ) kept[i--] = true;
                    do
                    {
                        i = 0;
                        for( int j = 0; j < nbTag; ++j )
                        {
                            if( kept[j] ) v[i++] = _tags[j];
                        }
                        Debug.Assert( i == _currentLength, "We kept the right number of tags." );
                        yield return _context.FindOrCreate( v, i );
                    }
                    while( Forward( kept, ref kMax ) );
                }
                while( --_currentLength > 1 );
            }
            // Special processing for currentLength = 1 (optimization)
            foreach( CKTag m in _tags ) yield return m;
            yield return _context.EmptyTag;
        }

        static bool Forward( bool[] kept, ref int kMax )
        {
            Debug.Assert( Array.FindLastIndex( kept, delegate( bool b ) { return b; } ) == kMax, "kMax maintains the last 'true' position." );
            kept[kMax] = false;
            if( ++kMax < kept.Length ) kept[kMax] = true;
            else
            {
                int maxIdx = kept.Length - 1;
                // Skips ending 'true' slots.
                int k = maxIdx - 1;
                while( k >= 0 && kept[k] ) --k;
                if( k < 0 ) return false;
                // Find the next 'true' (skips 'false' slots).
                int head = k;
                while( head >= 0 && !kept[head] ) --head;
                if( head < 0 ) return false;
                // Number of 'true' slots after the head.
                int nb = kept.Length - k;
                kept[head++] = false;
                while( --nb >= 0 ) kept[head++] = true;
                // Resets ending slots to 'false'.
                kMax = head - 1;
                while( head < maxIdx ) kept[head++] = false;
            }
            return true;
        }


    }
}
