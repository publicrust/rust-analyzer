using System;
using System.Collections.Generic;
using System.Linq;

namespace RustAnalyzer
{
    public class PluginMethodParameter
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public bool IsOptional { get; set; }
        public string DefaultValue { get; set; }
    }

    public class PluginMethod
    {
        public string Name { get; set; }
        public string ReturnType { get; set; }
        public List<PluginMethodParameter> Parameters { get; set; }
        public string Description { get; set; }
    }

    public class PluginConfiguration
    {
        public string PluginName { get; set; }
        public Dictionary<string, PluginMethod> Methods { get; set; }
    }

    public static class PluginMethodsConfiguration
    {
        private static readonly Dictionary<string, PluginConfiguration> Configurations =
            new Dictionary<string, PluginConfiguration>
            {
                {
                    "ImageLibrary",
                    new PluginConfiguration
                    {
                        PluginName = "ImageLibrary",
                        Methods = new Dictionary<string, PluginMethod>
                        {
                            {
                                "AddImage",
                                new PluginMethod
                                {
                                    Name = "AddImage",
                                    ReturnType = "bool",
                                    Parameters = new List<PluginMethodParameter>
                                    {
                                        new PluginMethodParameter { Name = "url", Type = "string" },
                                        new PluginMethodParameter
                                        {
                                            Name = "imageName",
                                            Type = "string",
                                        },
                                        new PluginMethodParameter
                                        {
                                            Name = "imageId",
                                            Type = "ulong",
                                        },
                                        new PluginMethodParameter
                                        {
                                            Name = "callback",
                                            Type = "Action",
                                            IsOptional = true,
                                            DefaultValue = "null",
                                        },
                                    },
                                    Description = "Adds an image from URL",
                                }
                            },
                            {
                                "AddImageData",
                                new PluginMethod
                                {
                                    Name = "AddImageData",
                                    ReturnType = "bool",
                                    Parameters = new List<PluginMethodParameter>
                                    {
                                        new PluginMethodParameter
                                        {
                                            Name = "imageName",
                                            Type = "string",
                                        },
                                        new PluginMethodParameter
                                        {
                                            Name = "array",
                                            Type = "byte[]",
                                        },
                                        new PluginMethodParameter
                                        {
                                            Name = "imageId",
                                            Type = "ulong",
                                        },
                                        new PluginMethodParameter
                                        {
                                            Name = "callback",
                                            Type = "Action",
                                            IsOptional = true,
                                            DefaultValue = "null",
                                        },
                                    },
                                    Description = "Adds an image from byte array",
                                }
                            },
                            {
                                "GetImageURL",
                                new PluginMethod
                                {
                                    Name = "GetImageURL",
                                    ReturnType = "string",
                                    Parameters = new List<PluginMethodParameter>
                                    {
                                        new PluginMethodParameter
                                        {
                                            Name = "imageName",
                                            Type = "string",
                                        },
                                        new PluginMethodParameter
                                        {
                                            Name = "imageId",
                                            Type = "ulong",
                                            IsOptional = true,
                                            DefaultValue = "0",
                                        },
                                    },
                                    Description = "Gets image URL by name",
                                }
                            },
                            {
                                "GetImage",
                                new PluginMethod
                                {
                                    Name = "GetImage",
                                    ReturnType = "string",
                                    Parameters = new List<PluginMethodParameter>
                                    {
                                        new PluginMethodParameter
                                        {
                                            Name = "imageName",
                                            Type = "string",
                                        },
                                        new PluginMethodParameter
                                        {
                                            Name = "imageId",
                                            Type = "ulong",
                                            IsOptional = true,
                                            DefaultValue = "0",
                                        },
                                        new PluginMethodParameter
                                        {
                                            Name = "returnUrl",
                                            Type = "bool",
                                            IsOptional = true,
                                            DefaultValue = "false",
                                        },
                                    },
                                    Description = "Gets image by name",
                                }
                            },
                            {
                                "GetImageList",
                                new PluginMethod
                                {
                                    Name = "GetImageList",
                                    ReturnType = "List<ulong>",
                                    Parameters = new List<PluginMethodParameter>
                                    {
                                        new PluginMethodParameter
                                        {
                                            Name = "name",
                                            Type = "string",
                                        },
                                    },
                                    Description = "Gets list of image IDs by name",
                                }
                            },
                            {
                                "GetSkinInfo",
                                new PluginMethod
                                {
                                    Name = "GetSkinInfo",
                                    ReturnType = "Dictionary<string, object>",
                                    Parameters = new List<PluginMethodParameter>
                                    {
                                        new PluginMethodParameter
                                        {
                                            Name = "name",
                                            Type = "string",
                                        },
                                        new PluginMethodParameter { Name = "id", Type = "ulong" },
                                    },
                                    Description = "Gets skin information",
                                }
                            },
                            {
                                "HasImage",
                                new PluginMethod
                                {
                                    Name = "HasImage",
                                    ReturnType = "bool",
                                    Parameters = new List<PluginMethodParameter>
                                    {
                                        new PluginMethodParameter
                                        {
                                            Name = "imageName",
                                            Type = "string",
                                        },
                                        new PluginMethodParameter
                                        {
                                            Name = "imageId",
                                            Type = "ulong",
                                        },
                                    },
                                    Description = "Checks if image exists",
                                }
                            },
                            {
                                "IsReady",
                                new PluginMethod
                                {
                                    Name = "IsReady",
                                    ReturnType = "bool",
                                    Parameters = new List<PluginMethodParameter>(),
                                    Description = "Checks if plugin is ready",
                                }
                            },
                            {
                                "ImportImageList",
                                new PluginMethod
                                {
                                    Name = "ImportImageList",
                                    ReturnType = "void",
                                    Parameters = new List<PluginMethodParameter>
                                    {
                                        new PluginMethodParameter
                                        {
                                            Name = "title",
                                            Type = "string",
                                        },
                                        new PluginMethodParameter
                                        {
                                            Name = "imageList",
                                            Type = "Dictionary<string, string>",
                                        },
                                        new PluginMethodParameter
                                        {
                                            Name = "imageId",
                                            Type = "ulong",
                                            IsOptional = true,
                                            DefaultValue = "0",
                                        },
                                        new PluginMethodParameter
                                        {
                                            Name = "replace",
                                            Type = "bool",
                                            IsOptional = true,
                                            DefaultValue = "false",
                                        },
                                        new PluginMethodParameter
                                        {
                                            Name = "callback",
                                            Type = "Action",
                                            IsOptional = true,
                                            DefaultValue = "null",
                                        },
                                    },
                                    Description = "Imports list of images",
                                }
                            },
                            {
                                "ImportItemList",
                                new PluginMethod
                                {
                                    Name = "ImportItemList",
                                    ReturnType = "void",
                                    Parameters = new List<PluginMethodParameter>
                                    {
                                        new PluginMethodParameter
                                        {
                                            Name = "title",
                                            Type = "string",
                                        },
                                        new PluginMethodParameter
                                        {
                                            Name = "itemList",
                                            Type = "Dictionary<string, Dictionary<ulong, string>>",
                                        },
                                        new PluginMethodParameter
                                        {
                                            Name = "replace",
                                            Type = "bool",
                                            IsOptional = true,
                                            DefaultValue = "false",
                                        },
                                        new PluginMethodParameter
                                        {
                                            Name = "callback",
                                            Type = "Action",
                                            IsOptional = true,
                                            DefaultValue = "null",
                                        },
                                    },
                                    Description = "Imports list of items",
                                }
                            },
                            {
                                "ImportImageData",
                                new PluginMethod
                                {
                                    ReturnType = "void",
                                    Parameters = new List<PluginMethodParameter>
                                    {
                                        new PluginMethodParameter
                                        {
                                            Name = "title",
                                            Type = "string",
                                        },
                                        new PluginMethodParameter
                                        {
                                            Name = "imageList",
                                            Type = "Dictionary<string, byte[]>",
                                        },
                                        new PluginMethodParameter
                                        {
                                            Name = "imageId",
                                            Type = "ulong",
                                            IsOptional = true,
                                            DefaultValue = "0",
                                        },
                                        new PluginMethodParameter
                                        {
                                            Name = "replace",
                                            Type = "bool",
                                            IsOptional = true,
                                            DefaultValue = "false",
                                        },
                                        new PluginMethodParameter
                                        {
                                            Name = "callback",
                                            Type = "Action",
                                            IsOptional = true,
                                            DefaultValue = "null",
                                        },
                                    },
                                }
                            },
                            {
                                "LoadImageList",
                                new PluginMethod
                                {
                                    ReturnType = "void",
                                    Parameters = new List<PluginMethodParameter>
                                    {
                                        new PluginMethodParameter
                                        {
                                            Name = "title",
                                            Type = "string",
                                        },
                                        new PluginMethodParameter
                                        {
                                            Name = "imageList",
                                            Type = "List<KeyValuePair<string, ulong>>",
                                        },
                                        new PluginMethodParameter
                                        {
                                            Name = "callback",
                                            Type = "Action",
                                            IsOptional = true,
                                            DefaultValue = "null",
                                        },
                                    },
                                }
                            },
                            {
                                "RemoveImage",
                                new PluginMethod
                                {
                                    ReturnType = "void",
                                    Parameters = new List<PluginMethodParameter>
                                    {
                                        new PluginMethodParameter
                                        {
                                            Name = "imageName",
                                            Type = "string",
                                        },
                                        new PluginMethodParameter
                                        {
                                            Name = "imageId",
                                            Type = "ulong",
                                        },
                                    },
                                }
                            },
                            {
                                "SendImage",
                                new PluginMethod
                                {
                                    ReturnType = "void",
                                    Parameters = new List<PluginMethodParameter>
                                    {
                                        new PluginMethodParameter
                                        {
                                            Name = "player",
                                            Type = "BasePlayer",
                                        },
                                        new PluginMethodParameter
                                        {
                                            Name = "imageName",
                                            Type = "string",
                                        },
                                        new PluginMethodParameter
                                        {
                                            Name = "imageId",
                                            Type = "ulong",
                                            IsOptional = true,
                                            DefaultValue = "0",
                                        },
                                    },
                                }
                            },
                        },
                    }
                },
                {
                    "CopyPaste",
                    new PluginConfiguration
                    {
                        PluginName = "CopyPaste",
                        Methods = new Dictionary<string, PluginMethod>
                        {
                            ["TryCopyFromSteamId"] = new PluginMethod
                            {
                                ReturnType = "object",
                                Parameters = new List<PluginMethodParameter>
                                {
                                    new PluginMethodParameter { Name = "userID", Type = "ulong" },
                                    new PluginMethodParameter
                                    {
                                        Name = "filename",
                                        Type = "string",
                                    },
                                    new PluginMethodParameter { Name = "args", Type = "string[]" },
                                },
                            },
                            ["TryPasteFromSteamId"] = new PluginMethod
                            {
                                ReturnType = "object",
                                Parameters = new List<PluginMethodParameter>
                                {
                                    new PluginMethodParameter { Name = "userID", Type = "ulong" },
                                    new PluginMethodParameter
                                    {
                                        Name = "filename",
                                        Type = "string",
                                    },
                                    new PluginMethodParameter { Name = "args", Type = "string[]" },
                                },
                            },
                            ["TryPasteFromVector3"] = new PluginMethod
                            {
                                ReturnType = "object",
                                Parameters = new List<PluginMethodParameter>
                                {
                                    new PluginMethodParameter { Name = "pos", Type = "Vector3" },
                                    new PluginMethodParameter
                                    {
                                        Name = "rotationCorrection",
                                        Type = "float",
                                    },
                                    new PluginMethodParameter
                                    {
                                        Name = "filename",
                                        Type = "string",
                                    },
                                    new PluginMethodParameter { Name = "args", Type = "string[]" },
                                },
                            },
                        },
                    }
                },
            };

        public static PluginConfiguration GetConfiguration(string pluginName)
        {
            return Configurations.TryGetValue(pluginName, out var config) ? config : null;
        }

        public static PluginMethod GetMethod(string pluginName, string methodName)
        {
            var config = GetConfiguration(pluginName);
            return config?.Methods.TryGetValue(methodName, out var method) == true ? method : null;
        }

        public static bool HasPlugin(string pluginName)
        {
            return Configurations.ContainsKey(pluginName);
        }
    }
}
