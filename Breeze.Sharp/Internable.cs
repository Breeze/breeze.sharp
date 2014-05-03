
using System;

namespace Breeze.Sharp {

  
  public abstract class Internable : IJsonSerializable {

  
    protected Internable() {
      
    }

    public String Name {
      get;
      protected set;
    }

    internal JNode ToJNode() {
      // This ONLY works because of the immutability convention for all Internables
      if (_jNode == null) {
        _jNode = JNode.FromObject(this, true);
      }
      return _jNode;
    }

    JNode IJsonSerializable.ToJNode(object config) {
      return ToJNode();
    }

    public override bool Equals(object obj) {
      if (obj == this) return true;
      var other = obj as Internable;
      if (other == null) return false;
      return this.ToJNode().Equals(other.ToJNode());
    }

    internal bool IsInterned {
      get;
      set;
    }

    public override int GetHashCode() {
      // This ONLY works because of the immutability convention for all Validators.
      if (_hashCode == 0) {
        _hashCode = this.ToJNode().GetHashCode();
      }
      return _hashCode;
    }

    private JNode _jNode;
    private int _hashCode = 0;

  }


}
