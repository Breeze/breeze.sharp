using System;
using System.Collections;
using System.Collections.Generic;

namespace Breeze.Sharp.Core {

  

  /// <summary>
  /// Base class for a number of Breeze.sharp collections.
  /// </summary>
  /// <remarks>
  /// Similar to the HashSet but extensible via subclassing and with a  smaller api surface.
  /// Should be subclassed to expose unique collections where the consumer
  /// has the ability to add/remove items.
  /// </remarks>
  /// <typeparam name="U"></typeparam>
  public abstract class SetCollection<U> : ICollection<U> {
    /// <summary>
    /// Public ctor
    /// </summary>
    public SetCollection() {
      
    }

    /// <summary>
    /// Public ctor
    /// </summary>
    /// <param name="values"></param>
    public SetCollection(IEnumerable<U> values) {
      values.ForEach(v => this.Add(v));
    }

    /// <summary>
    /// Adds an item to the collection
    /// </summary>
    /// <remarks>
    /// Returns:
    //     true if the element is added; false if the element is already present.
    /// </remarks>
    /// <param name="value"></param>
    public virtual void Add(U value) {
      _set.Add( value);
    }

    /// <summary>
    /// Removes all elements from the collection. 
    /// </summary>
    public virtual void Clear() {
      _set.Clear();
    }

    /// <summary>
    ///  Determines whether this collection contains the specified element.
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public virtual bool Contains(U item) {
      return _set.Contains(item);
    }
    
    /// <summary>
    /// Copies the elements of this collection to an array, starting at the specified array index.
    /// </summary>
    /// <param name="array"></param>
    /// <param name="arrayIndex"></param>
    public void CopyTo(U[] array, int arrayIndex) {
      _set.CopyTo(array, arrayIndex);
    }

    /// <summary>
    /// Returns the number of items in this collection.
    /// </summary>
    public int Count {
      get { return _set.Count; }
    }

    public bool IsReadOnly {
      get { return false; }
    }

    /// <inheritDoc />
    public virtual bool Remove(U item) {
      return _set.Remove(item);
    }


    IEnumerator<U> IEnumerable<U>.GetEnumerator() {
      return _set.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() {
      return _set.GetEnumerator();
    }

    private HashSet<U> _set = new HashSet<U>();

  }

}
