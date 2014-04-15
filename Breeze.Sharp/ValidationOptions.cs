using System;
using System.Linq;

namespace Breeze.Sharp {
  // TODO: make immutable later

  /// <summary>
  /// A ValidationOptions instance is used to specify the conditions under which validation will be executed.
  /// This may be set via the <see cref="EntityManager.ValidationOptions"/> property.
  /// </summary>
  public class ValidationOptions {
    public ValidationOptions() {
      ValidationApplicability = ValidationApplicability.Default;
      // ValidationNotificationMode = NetClient.ValidationNotificationMode.Notify;
      
    }
    public ValidationApplicability ValidationApplicability { get; set; }

    public static ValidationOptions Default = new ValidationOptions();

  }

  /// <summary>
  /// An enum used to describe the conditions under which validation should occur.
  /// This value is set via the <see cref="ValidationOptions.ValidationApplicability"/>.
  /// </summary>
  [Flags]
  public enum ValidationApplicability {
    OnPropertyChange = 1,
    OnAttach = 2,
    OnQuery = 4,
    OnSave = 8,
    Default = OnPropertyChange | OnAttach | OnSave
  }

  // DON'T ADD back without discussing with Jay
  // Possiblity of throwing an exception while validating requires changes in a number of places
  // to insure other process aren't broken (example is during a query)
  //public enum ValidationNotificationMode {
  //  /// <summary>
  //  /// Causes notification through the <b>INotifyDataErrorInfo</b> interface.
  //  /// </summary>
  //  Notify = 1,

  //  /// <summary>
  //  /// Throws an exception.
  //  /// </summary>
  //  ThrowException = 2,

  //  /// <summary>
  //  /// Notify using <b>INotifyDataErrorInfo</b> and throw exception.
  //  /// </summary>
  //  NotifyAndThrowException = 3
  //}

}
