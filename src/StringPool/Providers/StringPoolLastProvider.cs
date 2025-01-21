using RustAnalyzer.src.Attributes;
using RustAnalyzer.src.StringPool.Interfaces;

namespace RustAnalyzer.src.StringPool.Providers
{
    [Version("LastVersion")]
    public class StringPoolLastProvider : BaseJsonStringPoolProvider
    {
        protected override string JsonContent => @"{
    ""assets/content/vehicles/boats/rhib/subents/rhib_storage"": 1187441255,
    ""assets/prefabs/deployable/large wood storage/box.wooden.large.prefab"": 1560881570,
    ""assets/prefabs/deployable/woodenbox/box.wooden.prefab"": 1560872478,
    ""assets/prefabs/misc/item drop/item_drop.prefab"": 545786656,
    ""assets/prefabs/misc/item drop/item_drop_backpack.prefab"": 545786656
}";
    }
} 