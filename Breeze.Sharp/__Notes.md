# Notes 
  
- Linq to OData - projection (select restrictions)
    - project into anon types only
	- no aliasing
	- can only project properties 

- EntityAspect EntityManager & EntityGroup properties are set if an entity was ever attached. 
	- This is not cleared when an entity is detached.  This assumption removes a lot  of special purpose logic.


- Only the MetadataStore and immutable types are ThreadSafe.  Any property returned by the MetadataStore 
must therefore also be either immutable or thread safe itself. (all StructuralTypes are effectively immutable)

- Unit tests are problematic because they do not run with a UI synchronization context which means that async calls within a test can return on 
a different thread then they started on.  However, most unit tests run safely despite this because they don't tend to run multiple threads simulataneously.
An exception to this is any call to Task.WhenAll or Task.WaitAll, use of these methods will require the test code to be wrapped in a UI synch context.

- ComplexType inheritance not yet supported.

- NamingConvention & MetadataMismatch event relationship
    - NamingConvention translates type and property names. 
    - MetadataMismatch is only fired during server side metadata retrieval after NamingConvention translations have already occured ( and allows you to ignore certain classes of mismatch) 

### Think about

- NoTracking option
    - A Breeze specific Http exception that include both a status code and a message.
- Enum for flags like InProcess, IsLoading etc ( probably not a good idea for booleans that have a concept of inheritance)
If moving MetadataInfo from .NET to JS and back NamingConventions are likely not compatible - so what do we do ???
ShortName map
NullEntity