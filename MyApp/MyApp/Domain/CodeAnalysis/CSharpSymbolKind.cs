namespace MyApp.Domain.CodeAnalysis
{
    public enum CSharpSymbolKind
    {
        Unknown = 0,
        Namespace = 1,
        Assembly = 2,
        Module = 3,
        Class = 4,
        Struct = 5,
        Interface = 6,
        Enum = 7,
        Delegate = 8,
        Record = 9,
        RecordStruct = 10,
        Field = 11,
        Property = 12,
        Method = 13,
        Constructor = 14,
        Destructor = 15,
        Operator = 16,
        Indexer = 17,
        Event = 18,
        EnumMember = 19,
        LocalVariable = 20,
        Parameter = 21,
        TypeParameter = 22,
        File = 23,
        Using = 24,
        Attribute = 25
    }
}
