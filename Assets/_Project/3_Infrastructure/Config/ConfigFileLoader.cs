using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Card.Core;
using Card.Domain.Config;

namespace Card.Infrastructure.Config
{
    /// <summary>
    /// 读取生成物 JSON（导入器写出方向的对应实现）。
    /// 只做文件 I/O：解析在 <see cref="JsonValue"/>、映射在 <see cref="ConfigJsonReader"/>；
    /// 缺文件、JSON 损坏、字段缺失都会汇成错误列表返回，不抛异常穿透游戏循环。
    /// </summary>
    public static class ConfigFileLoader
    {
        public static ConfigLoadResult Load(string directory)
        {
            Guard.NotNull(directory, nameof(directory));

            List<string> errors = new List<string>();
            JsonValue? cards = TryRead(directory, ConfigSchema.CardsFileName, errors);
            JsonValue? heroes = TryRead(directory, ConfigSchema.HeroesFileName, errors);
            JsonValue? heroPowers = TryRead(directory, ConfigSchema.HeroPowersFileName, errors);
            JsonValue? rarityWeights = TryRead(directory, ConfigSchema.RarityWeightsFileName, errors);
            JsonValue? gacha = TryRead(directory, ConfigSchema.GachaFileName, errors);
            JsonValue? rules = TryRead(directory, ConfigSchema.RulesFileName, errors);

            if (errors.Count > 0)
            {
                return ConfigLoadResult.Failure(errors);
            }

            try
            {
                return ConfigLoadResult.Success(
                    ConfigJsonReader.ReadBundle(cards!, heroes!, heroPowers!, rarityWeights!, gacha!, rules!));
            }
            catch (ConfigReadException exception)
            {
                errors.Add(exception.Message);
                return ConfigLoadResult.Failure(errors);
            }
        }

        private static JsonValue? TryRead(string directory, string fileName, List<string> errors)
        {
            string path = Path.Combine(directory, fileName);
            if (!File.Exists(path))
            {
                errors.Add("缺少生成物：" + fileName);
                return null;
            }

            try
            {
                return JsonValue.Parse(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (FormatException exception)
            {
                errors.Add(fileName + " 解析失败：" + exception.Message);
                return null;
            }
        }
    }
}
