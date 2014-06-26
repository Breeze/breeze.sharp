
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Breeze.Sharp {

  #region EntityChange base classes

  /// <summary>
  /// Base class for all Entity Changing/Changed event args.
  /// </summary>
  public abstract class EntityChangeEventArgs : EventArgs {

    /// <summary>
    /// Create an instance of this class.
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="entityAction"></param>
    /// <param name="actionEventArgs"></param>
    protected EntityChangeEventArgs(IEntity entity, EntityAction entityAction, EventArgs actionEventArgs = null) 
      : base() {
      Entity = entity;
      Action = entityAction;
      ActionEventArgs = actionEventArgs ?? EventArgs.Empty;
    }

    /// <summary>
    /// Returns the EntityAspect for the entity involved in the event.
    /// </summary>
    public EntityAspect EntityAspect {
      get { return Entity.EntityAspect; }
    }

    /// <summary>
    ///  The object that is changing or has been changed.
    /// </summary>
    public IEntity Entity {
      get;
      private set;
    }

    /// <summary>
    /// Action that caused this change.
    /// </summary>
    public EntityAction Action {
      get; 
      private set;
    }

    /// <summary>
    /// The EventArgs ( if any) that are associated with this specified EntityAction.
    /// </summary>
    public EventArgs ActionEventArgs {
      get; 
      private set;
    }

  }

  
  /// <summary>
  /// Provides information to the <see cref="E:Breeze.Sharp.EntityGroup.EntityChanging"/> event.
  /// </summary>
  /// <remarks>
  /// The <see cref="E:Breeze.Sharp.EntityGroup.EntityChanging"/> event fires whenever an action
  /// is about to take place on an entity.
  /// You can set the <see cref="P:Breeze.Sharp.EntityChangingEventArgs.Cancel"/> property to true in your handler to stop further 
  /// processing of the change.  See the <see cref="EntityAction"/> for the action to
  /// be performed.
  /// </remarks>
  public class EntityChangingEventArgs : EntityChangeEventArgs {

    /// <summary>
    /// Ctor.
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="action"></param>
    /// <param name="actionEventArgs"></param>
    public EntityChangingEventArgs(IEntity entity, EntityAction action, EventArgs actionEventArgs)
      : base(entity, action, actionEventArgs) {
      Cancel = false;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the event should be canceled.
    /// </summary>
    /// <value>True if the event should be canceled; otherwise, False.</value>
    public bool Cancel { get; set; }


  }
  

  /// <summary>
  /// Provides information to the <see cref="E:Breeze.Sharp.EntityGroup.EntityChanged"/> event.
  /// </summary>
  /// <remarks>
  /// The <see cref="E:Breeze.Sharp.EntityGroup.EntityChanged"/> event fires whenever an action
  /// has taken place on an entity. See the <see cref="EntityAction"/> for the action performed.
  /// </remarks>
  public class EntityChangedEventArgs : EntityChangeEventArgs {

    /// <summary>
    /// Ctor.
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="action"></param>
    /// <param name="actionEventArgs"></param>
    public EntityChangedEventArgs(IEntity entity, EntityAction action, EventArgs actionEventArgs)
      : base(entity, action, actionEventArgs ) {
      
    }

    
  }

  #endregion

  //#region EntityPropertyChangingEventArgs
  ///// <summary>
  ///// Provides information to the <see cref="E:Breeze.Sharp.EntityGroup.EntityPropertyChanging"/> event.
  ///// </summary>
  ///// <remarks>
  ///// The <see cref="E:Breeze.Sharp.EntityGroup.EntityPropertyChanging"/> event fires whenever a property
  ///// setter is called on an entity in your model before the new value is set.
  ///// You can set the <see cref="P:Breeze.Sharp.EntityPropertyChangingEventArgs.Cancel"/> property to true in your handler to stop further 
  ///// processing of the change.  
  ///// </remarks>
  //public class EntityPropertyChangingEventArgs : EntityChangeCancelEventArgs {

  //  /// <summary>
  //  /// Ctor.
  //  /// </summary>
  //  /// <param name="entityAspect"></param>
  //  /// <param name="property"></param>
  //  /// <param name="value"></param>
  //  public EntityPropertyChangingEventArgs(IEntity entity, StructuralProperty property, object proposedValue)
  //    : base(entity) {
  //    Property = property;
  //    ProposedValue = proposedValue;
  //    Cancel = false;
  //  }

  //  /// <summary>
  //  /// 
  //  /// </summary>
  //  /// <param name="entity"></param>
  //  /// <param name="property"></param>
  //  /// <param name="proposedValue"></param>
  //  /// <param name="nestedProperty"></param>
  //  /// <param name="nestedProposedValue"></param>
  //  public EntityPropertyChangingEventArgs(IEntity entity, StructuralProperty property, IComplexObject proposedValue, StructuralProperty nestedProperty, object nestedProposedValue)
  //    : base(entity) {

  //    Property = property;
  //    ProposedValue = proposedValue;
  //    NestedProperty = nestedProperty;
  //    NestedProposedValue = nestedProposedValue;
  //    Cancel = false;
  //  }


  //  /// <summary>
  //  /// Property that is changing.
  //  /// </summary>
  //  public StructuralProperty Property { get; private set; }

  //  /// <summary>
  //  /// Gets or sets the proposed value of the property that is changing. 
  //  /// </summary>
  //  public object ProposedValue { get; set; }

  //  /// <summary>
  //  /// The local parent property that is actually being changed. Will be different from <see cref="Property"/> when a complex object is involved.
  //  /// </summary>
  //  public StructuralProperty NestedProperty { get; private set; }

  //  /// <summary>
  //  /// The actual value that is being proposed - only avail if the property is on a complex object
  //  /// </summary>
  //  public Object NestedProposedValue { get; private set; }


  //}
  //#endregion

  //#region EntityPropertyChangedEventArgs
  ///// <summary>
  ///// Provides information to the <see cref="E:Breeze.Sharp.EntityGroup.EntityPropertyChanged"/> event.
  ///// </summary>
  ///// <remarks>
  ///// The <see cref="E:Breeze.Sharp.EntityGroup.EntityPropertyChanged"/> event fires whenever a property
  ///// setter is called on an entity in your model after the new value is set.
  ///// </remarks>
  //public class EntityPropertyChangedEventArgs : EntityChangeEventArgs {
  //  /// <summary>
  //  /// Public ctor.
  //  /// </summary>
  //  /// <param name="entityAspect"></param>
  //  /// <param name="property"></param>
  //  /// <param name="newValue"></param>
  //  public EntityPropertyChangedEventArgs(IEntity entity, StructuralProperty property, object newValue)
  //    : base(entity) {
  //    Property = property;
  //    NewValue = newValue;
  //  }

  //   public EntityPropertyChangedEventArgs(IEntity entity, StructuralProperty property, IComplexObject newValue, StructuralProperty nestedProperty, object nestedNewValue)
  //    : base(entity) {

  //    Property = property;
  //    NewValue = newValue;
  //    NestedProperty = nestedProperty;
  //    NestedNewValue = nestedNewValue;
  //  }

  //  /// <summary>
  //  /// Property whose value has changed.
  //  /// </summary>
  //  public StructuralProperty Property { get; private set; }

  //  /// <summary>
  //  /// The value that was just set.
  //  /// </summary>
  //  public object NewValue { get; set; }


  //  /// <summary>
  //  /// For complex objects this is the local complex object property that changed. 
  //  /// </summary>
  //  public StructuralProperty NestedProperty { get; private set; }

  //  /// <summary>
  //  /// The nested value that was just set.
  //  /// </summary>
  //  public object NestedNewValue { get; set; }


  //}
  //#endregion
}
