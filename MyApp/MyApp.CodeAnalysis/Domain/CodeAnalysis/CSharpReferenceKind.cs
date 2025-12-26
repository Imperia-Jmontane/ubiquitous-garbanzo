namespace MyApp.CodeAnalysis.Domain.CodeAnalysis
{
    public enum CSharpReferenceKind
    {
        Unknown = 0,
        Inheritance = 1,
        InterfaceImplementation = 2,
        Call = 3,
        TypeUsage = 4,
        Override = 5,
        FieldAccess = 6,
        PropertyAccess = 7,
        EventAccess = 8,
        Contains = 9,
        Import = 10,
        TypeArgument = 11,
        AttributeUsage = 12,
        Instantiation = 13,
        Cast = 14,
        Throw = 15,
        Catch = 16
    }
}
