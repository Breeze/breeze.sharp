using System;

namespace Breeze.Sharp {
  public class DomainException : Exception {
    public string Description { get; }

    public DomainException(string message) : base(message) {
      
    }

    public DomainException(string description, string message) : base(message) {
      Description = description;

    }

    public DomainException(string description, string message, Exception innerException) : base(message, innerException) {
      Description = description;

    }
  }
}