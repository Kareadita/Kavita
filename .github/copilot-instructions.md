## Coding Guidelines
- Anytime you don't use {} on an if statement, it must be on one line and MUST be a jump operation (return/continue/break). All other times, you must use {} and be on 2 lines.
- Use var whenever possible
- return statements should generally have a newline above them
Examples:
```csharp
# Case when okay - simple logic flow
var a = 2 + 3;
return a;

# Case when needs newline - complex logic is grouped together
var a = b + c;
_imageService.Resize(...);

return;
```
- Operation (+,-,*, etc) should always have spaces around it; I.e. `a + b` not `a+b`.
- When setting href directectly (not using Angulars routing) it should always be prefixed with baseURL
