using System.Diagnostics;
using System.Text;
using DevNexus.Client.Shared.Helpers;
using DevNexus.Shared.DTOs;
using DevNexus.Shared.Enums;

namespace DevNexus.Client.Shared.Services.Chat;

/// <summary>
/// 差量块索引器 - 负责高效管理和索引流式块
/// 核心优化：从 O(n) 全量筛选到 O(1) 增量追加
/// </summary>
public sealed class BlockIndexer : IDisposable
{
    #region 核心索引

    /// <summary>
    /// 全部块列表（追加模式，保持完整历史）
    /// </summary>
    private readonly List<BlockDto> _allBlocks = new();
    
    /// <summary>
    /// 展示块列表（Terminal/Chart/InteractiveCard/Warning/Reference/Truncated）。
    /// </summary>
    private readonly List<BlockDto> _orderedBlocks = new();
    
    /// <summary>
    /// BlockId → _allBlocks 索引映射（O(1) 查找）
    /// </summary>
    private readonly Dictionary<Guid, int> _blockIdToAllIndex = new();
    
    /// <summary>
    /// BlockId → _orderedBlocks 索引映射（O(1) 查找）
    /// </summary>
    private readonly Dictionary<Guid, int> _blockIdToOrderedIndex = new();

    #endregion

    #region 内容缓存

    /// <summary>
    /// 文本内容增量拼接缓存（StringBuilder 避免重复分配）
    /// </summary>
    private readonly StringBuilder _textContent = new(8192);
    
    /// <summary>
    /// 思维链内容增量拼接缓存
    /// </summary>
    private readonly StringBuilder _thinkingContent = new(4096);

    #endregion

    #region 扫描游标与状态

    /// <summary>
    /// 思维链是否需要重建（发生更新操作时标记）
    /// </summary>
    private bool _needsThinkingRebuild = false;

    #endregion

    #region 性能指标

    /// <summary>
    /// 文本块数量统计
    /// </summary>
    private int _textDeltaCount = 0;
    
    /// <summary>
    /// 思维块数量统计
    /// </summary>
    private int _thinkingCount = 0;
    
    /// <summary>
    /// 展示块数量统计
    /// </summary>
    private int _orderedBlockCount = 0;
    
    /// <summary>
    /// 更新操作数量统计
    /// </summary>
    private int _updateOperationCount = 0;
    
    /// <summary>
    /// AddBlock 耗时统计
    /// </summary>
    private readonly Stopwatch _addBlockStopwatch = new();
    
    /// <summary>
    /// AddBlock 总耗时（Ticks）
    /// </summary>
    private long _totalAddBlockTicks = 0;
    
    /// <summary>
    /// AddBlock 调用次数
    /// </summary>
    private int _addBlockCallCount = 0;

    #endregion

    #region 公共方法

    /// <summary>
    /// 增量添加块 - O(1) 时间复杂度
    /// </summary>
    /// <param name="block">要添加的块</param>
    public void AddBlock(BlockDto block)
    {
        _addBlockStopwatch.Restart();

        try
        {
            // 0. 去重检查：如果 BlockId 已存在，跳过
            if (_blockIdToAllIndex.ContainsKey(block.BlockId))
            {
                return;
            }

            // 1. 添加到全部块列表
            _allBlocks.Add(block);
            _blockIdToAllIndex[block.BlockId] = _allBlocks.Count - 1;

            // 2. 根据类型增量处理
            switch (block.BlockType)
            {
                case BlockType.TextDelta:
                    // 文本块直接追加到 StringBuilder
                    _textContent.Append(block.Content);
                    _textDeltaCount++;
                    AddOrAppendSequentialBlock(block);
                    break;

                case BlockType.Thinking:
                    // 思维链块追加到 StringBuilder
                    MetadataHelper.AppendThoughtSegment(_thinkingContent, block.Content);
                    _thinkingCount++;
                    AddOrAppendSequentialBlock(block);
                    break;

                case BlockType.Terminal:
                case BlockType.Chart:
                case BlockType.InteractiveCard:
                case BlockType.Warning:
                case BlockType.Reference:
                case BlockType.Truncated:
                    // 展示块添加到 OrderedBlocks
                    AddToOrderedBlocks(block);
                    break;
            }
        }
        finally
        {
            _addBlockStopwatch.Stop();
            _totalAddBlockTicks += _addBlockStopwatch.ElapsedTicks;
            _addBlockCallCount++;
        }
    }

    /// <summary>
    /// 原位更新块 - 避免重建列表
    /// </summary>
    /// <param name="blockId">要更新的块 ID</param>
    /// <param name="block">新的块数据</param>
    /// <returns>更新是否成功</returns>
    public bool UpdateBlock(Guid blockId, BlockDto block)
    {
        // 1. 快速定位：O(1) 查找
        if (!_blockIdToAllIndex.TryGetValue(blockId, out var allIndex))
            return false;

        var oldBlock = _allBlocks[allIndex];

        // 2. 判断是否影响展示块索引
        bool wasOrdered = IsOrderedBlockType(oldBlock.BlockType);
        bool isOrdered = IsOrderedBlockType(block.BlockType);

        // 3. 原位替换
        _allBlocks[allIndex] = block;

        // 4. 更新展示块索引（如果需要）
        if (wasOrdered && isOrdered && _blockIdToOrderedIndex.TryGetValue(blockId, out var orderedIndex))
        {
            _orderedBlocks[orderedIndex] = block;
        }
        else if (wasOrdered && !isOrdered)
        {
            RemoveFromOrderedBlocks(blockId);
        }
        else if (!wasOrdered && isOrdered)
        {
            AddToOrderedBlocks(block);
        }

        // 5. 标记需要重新处理（思维链、文本可能变化）
        _needsThinkingRebuild = true;
        _updateOperationCount++;

        return true;
    }

    /// <summary>
    /// 获取完整内容（含 Thinking 标签）
    /// </summary>
    /// <returns>完整的消息内容</returns>
    public string GetFullContent()
    {
        // 快速路径：无 Thinking 时直接返回文本
        if (_thinkingCount == 0)
            return _textContent.ToString();

        // 重建思维链（仅在需要时）
        if (_needsThinkingRebuild)
        {
            RebuildThinkingContent();
            _needsThinkingRebuild = false;
        }

        // 组合内容
        if (_thinkingContent.Length == 0)
            return _textContent.ToString();

        return $"<think>{_thinkingContent}</think>\n{_textContent}";
    }

    /// <summary>
    /// 获取展示块列表（只读视图，避免不必要的拷贝）
    /// </summary>
    /// <returns>展示块的只读列表</returns>
    public IReadOnlyList<BlockDto> GetOrderedBlocks()
    {
        return _orderedBlocks;
    }

    /// <summary>
    /// 清空所有数据和索引
    /// </summary>
    public void Clear()
    {
        _allBlocks.Clear();
        _orderedBlocks.Clear();
        _blockIdToAllIndex.Clear();
        _blockIdToOrderedIndex.Clear();
        _textContent.Clear();
        _thinkingContent.Clear();
        
        _needsThinkingRebuild = false;
        _textDeltaCount = 0;
        _thinkingCount = 0;
        _orderedBlockCount = 0;
        _updateOperationCount = 0;
        
        // 性能指标不清空，保留历史统计
    }

    /// <summary>
    /// 获取性能指标
    /// </summary>
    /// <returns>当前性能指标快照</returns>
    public BlockIndexerMetrics GetMetrics()
    {
        return new BlockIndexerMetrics
        {
            TotalBlockCount = _allBlocks.Count,
            TextDeltaCount = _textDeltaCount,
            ThinkingCount = _thinkingCount,
            OrderedBlockCount = _orderedBlockCount,
            UpdateOperationCount = _updateOperationCount,
            TextContentLength = _textContent.Length,
            ThinkingContentLength = _thinkingContent.Length,
            NeedsThinkingRebuild = _needsThinkingRebuild,
            AverageAddBlockTimeMs = GetAverageAddBlockTimeMs(),
            AddBlockCallCount = _addBlockCallCount
        };
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        _allBlocks.Clear();
        _orderedBlocks.Clear();
        _blockIdToAllIndex.Clear();
        _blockIdToOrderedIndex.Clear();
        _textContent.Clear();
        _thinkingContent.Clear();
    }

    #endregion

    #region 私有辅助方法

    /// <summary>
    /// 重建思维链内容（仅在更新操作后需要）
    /// </summary>
    private void RebuildThinkingContent()
    {
        _thinkingContent.Clear();
        foreach (var block in _allBlocks)
        {
            if (block.BlockType == BlockType.Thinking)
            {
                MetadataHelper.AppendThoughtSegment(_thinkingContent, block.Content);
            }
        }
    }

    /// <summary>
    /// 添加块到展示块列表
    /// </summary>
    /// <param name="block">要添加的块</param>
    private void AddToOrderedBlocks(BlockDto block)
    {
        _orderedBlocks.Add(block);
        _blockIdToOrderedIndex[block.BlockId] = _orderedBlocks.Count - 1;
        _orderedBlockCount++;
    }

    /// <summary>
    /// 将连续的文本/思考块合并到同一展示块中，保留与终端块的交错顺序。
    /// </summary>
    /// <param name="block">要追加的块</param>
    private void AddOrAppendSequentialBlock(BlockDto block)
    {
        if (_orderedBlocks.Count > 0)
        {
            var lastIndex = _orderedBlocks.Count - 1;
            var lastBlock = _orderedBlocks[lastIndex];
            if (lastBlock.BlockType == block.BlockType)
            {
                lastBlock.Content = $"{lastBlock.Content}{block.Content}";
                lastBlock.IsLast = block.IsLast;
                _blockIdToOrderedIndex[block.BlockId] = lastIndex;
                return;
            }
        }

        AddToOrderedBlocks(block);
    }

    /// <summary>
    /// 从展示块列表中移除块
    /// </summary>
    /// <param name="blockId">要移除的块 ID</param>
    private void RemoveFromOrderedBlocks(Guid blockId)
    {
        if (!_blockIdToOrderedIndex.TryGetValue(blockId, out var index))
            return;

        _orderedBlocks.RemoveAt(index);
        _blockIdToOrderedIndex.Remove(blockId);
        _orderedBlockCount--;

        // 重建索引（后续块的索引需要减 1）
        for (int i = index; i < _orderedBlocks.Count; i++)
        {
            _blockIdToOrderedIndex[_orderedBlocks[i].BlockId] = i;
        }
    }

    /// <summary>
    /// 判断块类型是否为展示块
    /// </summary>
    /// <param name="blockType">块类型</param>
    /// <returns>是否为展示块</returns>
    private static bool IsOrderedBlockType(BlockType blockType)
    {
        return blockType == BlockType.TextDelta ||
               blockType == BlockType.Thinking ||
               blockType == BlockType.Terminal ||
               blockType == BlockType.Chart ||
               blockType == BlockType.InteractiveCard ||
               blockType == BlockType.Warning ||
               blockType == BlockType.Reference ||
               blockType == BlockType.Truncated;
    }

    /// <summary>
    /// 获取 AddBlock 平均耗时
    /// </summary>
    /// <returns>平均耗时（毫秒）</returns>
    private double GetAverageAddBlockTimeMs()
    {
        if (_addBlockCallCount == 0)
            return 0;

        return (_totalAddBlockTicks / (double)_addBlockCallCount) / TimeSpan.TicksPerMillisecond;
    }

    #endregion
}
