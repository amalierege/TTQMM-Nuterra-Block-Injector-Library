﻿using System;
using System.Collections.Generic;
using System.Reflection;
using Harmony;
using UnityEngine;

namespace Nuterra.BlockInjector
{
    public static class BlockLoader
    {
        internal class Timer : MonoBehaviour
        {
            internal static string blocks = "";
            void OnGUI()
            {
                if (blocks != "")
                    GUILayout.Label("Loaded Blocks: "+blocks);
                if (Singleton.Manager<ManSplashScreen>.inst.HasExited)
                    UnityEngine.GameObject.Destroy(this.gameObject);
            }

            void Start()
            {
                Singleton.DoOnceAfterStart(Wait);
            }
            void Wait()
            {
                Invoke("Doit", 5f);
            }
            void Doit()
            {
                Console.WriteLine("The block injector is ready!");
                MakeReady();
            }
        }

        internal class OverrideInputHandler : MonoBehaviour
        {
            void Update()
            {
                if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.B))
                {
                    DirectoryBlockLoader.LoadBlocks();
                }
            }
            internal static void INIT()
            {
                new GameObject().AddComponent<OverrideInputHandler>();
                AcceptOverwrite = true;
            }
        }

        public static bool AcceptOverwrite;

        public static readonly Dictionary<int, CustomBlock> CustomBlocks = new Dictionary<int, CustomBlock>();
        public static readonly Dictionary<int, CustomChunk> CustomChunks = new Dictionary<int, CustomChunk>();

        public static bool Register(CustomBlock block)
        {
            try
            {
                int blockID = block.BlockID;
                bool Overwriting = AcceptOverwrite && CustomBlocks.ContainsKey(blockID);
                Console.WriteLine($"Registering block: {block.GetType()} #{block.BlockID} '{block.Name}'");
                Timer.blocks += $"\n - #{blockID} - \"{block.Name}\"";
                ManSpawn spawnManager = ManSpawn.inst;
                if (!Overwriting)
                {
                    if (CustomBlocks.ContainsKey(blockID))
                    {
                        Timer.blocks += " - FAILED: Custom Block already exists!";
                        Console.WriteLine("Registering block failed: A block with the same ID already exists");
                        return false;
                    }
                    bool BlockExists = spawnManager.IsValidBlockToSpawn((BlockTypes)blockID);
                    if (BlockExists)
                    {
                        Timer.blocks += " - ID already exists";
                        Console.WriteLine("Registering block incomplete: A block with the same ID already exists");
                        return false;
                    }
                    CustomBlocks.Add(blockID, block);
                }
                else
                {
                    CustomBlocks[blockID] = block;
                }
                int hashCode = ItemTypeInfo.GetHashCode(ObjectTypes.Block, blockID);
                spawnManager.VisibleTypeInfo.SetDescriptor<FactionSubTypes>(hashCode, block.Faction);
                spawnManager.VisibleTypeInfo.SetDescriptor<BlockCategories>(hashCode, block.Category);
                spawnManager.VisibleTypeInfo.SetDescriptor<BlockRarity>(hashCode, block.Rarity);
                try
                {
                    if (Overwriting)
                    {
                        var prefabs = (BlockPrefabs.GetValue(ManSpawn.inst) as Dictionary<int, Transform>);
                        prefabs[blockID] = block.Prefab.transform;
                    }
                    else
                    {
                        AddBlockToDictionary.Invoke(spawnManager, new object[] { block.Prefab });
                        (LoadedBlocks.GetValue(ManSpawn.inst) as List<BlockTypes>).Add((BlockTypes)block.BlockID);
                        (LoadedActiveBlocks.GetValue(ManSpawn.inst) as List<BlockTypes>).Add((BlockTypes)block.BlockID);
                    }

                    var m_BlockPriceLookup = BlockPriceLookup.GetValue(RecipeManager.inst) as Dictionary<int, int>;
                    if (m_BlockPriceLookup.ContainsKey(blockID)) m_BlockPriceLookup[blockID] = block.Price;
                    else m_BlockPriceLookup.Add(blockID, block.Price);
                    return true;
                }
                catch (Exception E)
                {
                    Console.WriteLine(E.Message + "\n" + E.StackTrace);
                    if (E.InnerException != null)
                        Console.WriteLine(E.InnerException.Message + "\n" + E.InnerException.StackTrace);
                    Timer.blocks += " FAILED: " + E.InnerException?.Message;
                    return false;
                }
            }
            catch (Exception E)
            {
                Console.WriteLine(E.Message + "\n" + E.StackTrace + "\n" + E.InnerException?.Message);
                if (E.InnerException != null)
                {
                    Timer.blocks += " - FAILED: " + E.InnerException?.Message;
                }
                else
                {
                    Timer.blocks += " - FAILED: " + E.Message;
                }
                return false;
            }
        }
        static readonly FieldInfo LoadedBlocks = typeof(ManSpawn).GetField("m_LoadedBlocks", System.Reflection.BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo LoadedActiveBlocks = typeof(ManSpawn).GetField("m_LoadedActiveBlocks", System.Reflection.BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly MethodInfo AddBlockToDictionary = typeof(ManSpawn).GetMethod("AddBlockToDictionary", System.Reflection.BindingFlags.NonPublic | BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        static readonly FieldInfo BlockPrefabs = typeof(ManSpawn).GetField("m_BlockPrefabs", System.Reflection.BindingFlags.NonPublic | BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        static readonly FieldInfo BlockPriceLookup = typeof(RecipeManager).GetField("m_BlockPriceLookup", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        private static bool Ready = false;
        private static event Action PostStartEvent;

        public static void DelayAfterSingleton(Action ActionToDelay)
        {
            if (Ready)
            {
                ActionToDelay();
            }
            else
            {
                PostStartEvent += ActionToDelay;
            }
        }

        internal static void MakeReady()
        {
            Ready = true;
            if (PostStartEvent != null)
            {
                PostStartEvent();
            }
        }

        public static void Register(CustomChunk chunk)
        {
            Console.WriteLine($"Registering chunk: {chunk.GetType()} #{chunk.ChunkID} '{chunk.Name}'");
            CustomChunks.Add(chunk.ChunkID, chunk);
            ResourceTable table = ResourceManager.inst.resourceTable;
            ResourceTable.Definition[] definitions = table.resources;
            Array.Resize(ref definitions, definitions.Length);
            definitions[definitions.Length - 1] = new ResourceTable.Definition() { name = chunk.Name, basePrefab = chunk.BasePrefab, frictionDynamic = chunk.FrictionDynamic, frictionStatic = chunk.FrictionStatic, mass = chunk.Mass, m_ChunkType = (ChunkTypes)chunk.ChunkID, restitution = chunk.Restitution, saleValue = chunk.SaleValue };
        }

        public static void PostModsLoaded()
        {
            new GameObject().AddComponent<Timer>();
            BlockExamples.Load();
            DirectoryBlockLoader.LoadBlocks();
            var harmony = HarmonyInstance.Create("nuterra.block.injector");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            PostStartEvent += NetHandler.Patches.INIT;
            PostStartEvent += OverrideInputHandler.INIT;
        }

        internal class Patches
        {
            //[HarmonyPatch(typeof(ManSpawn), "AddBlockToDictionary")]
            //private static class DisallowGameBlockLoading
            //{
            //    private static bool Prefix(GameObject blockPrefab)
            //    {
            //        if (blockPrefab == null) return true;
            //        var Visible = blockPrefab.GetComponent<Visible>();
            //        if (Visible == null || Visible.m_ItemType.ObjectType != ObjectTypes.Block) return true;
            //        if (BlockPrefabBuilder.OverrideValidity.TryGetValue(Visible.m_ItemType.ItemType, out string name) && name != blockPrefab.name) return false;
            //        return true;
            //    }
            //}


            //public static bool Catching = false;
            //[HarmonyPatch(typeof(TTNetworkManager), "AddSpawnableType")]
            //private static class CatchHexRepeat
            //{
            //    private static bool Prefix(ref TTNetworkManager __instance, Transform prefab)
            //    {
            //        if (Catching)
            //        {
            //            Catching = false;
            //            try
            //            {
            //                typeof(TTNetworkManager).GetMethod("AddSpawnableType").Invoke(__instance, new object[] { prefab });
            //            }
            //            catch
            //            {
            //                try
            //                {
            //                    var hex = prefab.GetComponent<UnityEngine.Networking.NetworkIdentity>().assetId.ToString();
            //                    Console.WriteLine($"Hex code {hex} is unusable");
            //                    if (Timer.blocks != "" && hex != "")
            //                        Timer.blocks += " - Bad hex code (Can be ignored)";
            //                }
            //                catch
            //                {
            //                    Console.WriteLine("Could not print hex error!");
            //                }
            //            }
            //            return false;
            //        }
            //        return true;
            //    }
            //}

            static Type BTT = typeof(BlockTypes);

            [HarmonyPatch(typeof(ManSpawn), "IsBlockAvailableOnPlatform")]
            private static class TableFix
            {
                private static void Postfix(ref bool __result, BlockTypes blockType)
                {
                    if (!__result && !Enum.IsDefined(BTT, blockType)) __result = true;
                }
            }

            [HarmonyPatch(typeof(ModeCoOpCreative), "CheckBlockAllowed")]
            private static class TableFixCoOp
            {
                private static void Postfix(ref bool __result, BlockTypes blockType)
                {
                    if (!__result && !Enum.IsDefined(BTT, blockType)) __result = true;
                }
            }

            [HarmonyPatch(typeof(StringLookup), "GetString")]
            private static class OnStringLookup
            {
                private static bool Prefix(ref string __result, int itemType, LocalisationEnums.StringBanks stringBank)
                {
                    string result = "";
                    if (ResourceLookup_OnStringLookup(stringBank, itemType, ref result))
                    {
                        __result = result;
                        return false;
                    }
                    return true;
                }
            }
            [HarmonyPatch(typeof(SpriteFetcher), "GetSprite", new Type[] { typeof(ObjectTypes), typeof(int) })]
            private static class OnSpriteLookup
            {
                private static bool Prefix(ref UnityEngine.Sprite __result, ObjectTypes objectType, int itemType)
                {
                    UnityEngine.Sprite result = null;
                    if (ResourceLookup_OnSpriteLookup(objectType, itemType, ref result))
                    {
                        __result = result;
                        return false;
                    }
                    return true;
                }
            }
        }


        internal static void FixBlockUnlockTable(CustomBlock block)
        {
            try
            {
                ManLicenses.inst.DiscoverBlock((BlockTypes)block.BlockID);
                //For now, all custom blocks are level 1
                BindingFlags bind = BindingFlags.Instance | BindingFlags.NonPublic;
                Type T_BlockUnlockTable = typeof(BlockUnlockTable);
                Type CorpBlockData = T_BlockUnlockTable.GetNestedType("CorpBlockData", bind);
                Type GradeData = T_BlockUnlockTable.GetNestedType("GradeData", bind);
                Array blockList = T_BlockUnlockTable.GetField("m_CorpBlockList", bind).GetValue(ManLicenses.inst.GetBlockUnlockTable()) as Array;


                object corpData = blockList.GetValue((int)block.Faction);
                BlockUnlockTable.UnlockData[] unlocked = GradeData.GetField("m_BlockList").GetValue((CorpBlockData.GetField("m_GradeList").GetValue(corpData) as Array).GetValue(block.Grade)) as BlockUnlockTable.UnlockData[];
                Array.Resize(ref unlocked, unlocked.Length + 1);
                unlocked[unlocked.Length - 1] = new BlockUnlockTable.UnlockData
                {
                    m_BlockType = (BlockTypes)block.BlockID,
                    m_BasicBlock = true,
                    m_DontRewardOnLevelUp = true
                };
                GradeData.GetField("m_BlockList")
                    .SetValue(
                    (CorpBlockData.GetField("m_GradeList").GetValue(corpData) as Array).GetValue(block.Grade),
                    unlocked);

                ((T_BlockUnlockTable.GetField("m_CorpBlockLevelLookup", bind)
                    .GetValue(ManLicenses.inst.GetBlockUnlockTable()) as Array)
                    .GetValue((int)block.Faction) as Dictionary<BlockTypes, int>)
                    .Add((BlockTypes)block.BlockID, block.Grade);
                ManLicenses.inst.DiscoverBlock((BlockTypes)block.BlockID);
            }
            catch (Exception E)
            {
                Timer.blocks += " - FAILED: Could not add to block table. Could it be the grade level?";
                Console.WriteLine("Registering block failed: Could not add to block table. " + E.Message);
            }
        }

        private static bool ResourceLookup_OnSpriteLookup(ObjectTypes objectType, int itemType, ref UnityEngine.Sprite result)
        {
            if (objectType == ObjectTypes.Block)
            {
                CustomBlock block;
                if (CustomBlocks.TryGetValue(itemType, out block))
                {
                    result = block.DisplaySprite;
                    return result != null;
                }
            }
            else if (objectType == ObjectTypes.Chunk)
            {
                CustomBlock block;
                if (CustomBlocks.TryGetValue(itemType, out block))
                {
                    result = block.DisplaySprite;
                    return result != null;
                }
            }
            return false;
        }

        private static bool ResourceLookup_OnStringLookup(LocalisationEnums.StringBanks StringBank, int EnumValue, ref string Result)
        {
            CustomBlock block;
            CustomChunk chunk;
            switch (StringBank)
            {
                case LocalisationEnums.StringBanks.BlockNames:
                    if (CustomBlocks.TryGetValue(EnumValue, out block))
                    {
                        Result = block.Name;
                        return Result != null && Result != "";
                    }
                    break;

                case LocalisationEnums.StringBanks.BlockDescription:
                    if (CustomBlocks.TryGetValue(EnumValue, out block))
                    {
                        Result = block.Description;
                        return Result != null && Result != "";
                    }
                    break;
                case LocalisationEnums.StringBanks.ChunkName:
                    if (CustomChunks.TryGetValue(EnumValue, out chunk))
                    {
                        Result = chunk.Name;
                        return Result != null && Result != "";
                    }
                    break;

                case LocalisationEnums.StringBanks.ChunkDescription:
                    if (CustomChunks.TryGetValue(EnumValue, out chunk))
                    {
                        Result = chunk.Description;
                        return Result != null && Result != "";
                    }
                    break;
            }
            return false;
        }
    }
}