/**
 * DevNexus 文件解析器
 * 提供 CSV/Excel/TXT/MD 的前端解析能力，供 Blazor JSInterop 调用
 * 
 * 依赖:
 * - PapaParse 5.5.3 (CSV 解析)
 * - SheetJS/xlsx 0.19+ (Excel 解析)
 * 
 * 版本: 1.0.0
 * 最后更新: 2025-12-27
 */

window.DevNexusFileParser = {
    /**
     * 解析 CSV 文件内容
     * @param {string} fileContent - CSV 文本内容
     * @param {string} fileName - 原始文件名
     * @returns {Object} SmartDocument 格式的解析结果
     */
    parseCSV: async function (fileContent, fileName) {
        const startTime = performance.now();
        let contentHash = null;
        
        try {
            // 计算内容哈希（用于缓存和去重）
            contentHash = await this._calculateSHA256(fileContent);
            
            // 配置解析选项
            const result = Papa.parse(fileContent, {
                header: true,
                dynamicTyping: true,
                skipEmptyLines: true,
                trimHeaders: true,
                transformHeader: (header) => this._cleanHeader(header),
                transform: (value) => this._detectMaliciousContent(value) // 安全防护
            });

            const headers = result.meta.fields || [];
            const rowCount = result.data.length;

            // 质量评估
            const quality = this._assessQuality(result, 'csv', fileName);
            
            // 智能采样（前20%+后20%+随机60%）
            const sampledData = this._smartSampleTable(result.data, 1000);
            
            // 生成 CSV 表示
            const csvRepresentation = Papa.unparse(sampledData, { header: true });

            const totalTime = performance.now() - startTime;

            return {
                success: true,
                smartDocument: {
                    fileId: this._generateUUID(),
                    fileName: fileName,
                    mimeType: 'text/csv',
                    sizeBytes: new Blob([fileContent]).size,
                    contentHash: contentHash,
                    createdAt: new Date().toISOString(),
                    parsedAt: new Date().toISOString(),
                    content: {
                        contentType: 'table',
                        csvRepresentation: csvRepresentation,
                        headers: headers,
                        rowCount: rowCount,
                        columnCount: headers.length,
                        summary: this._generateTableSummary(rowCount, headers, 'csv'),
                        stats: null, // 客户端无法计算详细统计，由后端补充
                        sheetNames: null // CSV 无多工作表概念
                    },
                    chunks: [
                        {
                            id: this._generateUUID(),
                            type: 2, // Table
                            content: csvRepresentation,
                            structuredData: JSON.stringify(sampledData),
                            metadata: {
                                rowCount: rowCount,
                                columnCount: headers.length
                            }
                        }
                    ],
                    parseInfo: {
                        strategy: 'client-papaparse',
                        processingTimeMs: Math.round(totalTime),
                        costUSD: 0, // 显式声明零成本
                        tokensUsed: 0,
                        modelUsed: null,
                        qualityScore: quality.score,
                        warnings: quality.warnings,
                        parsedBy: 'client'
                    }
                }
            };
        } catch (error) {
            return {
                success: false,
                errorMessage: error.message,
                fallbackRecommendation: true // 建议降级到后端解析
            };
        } finally {
            // 内存清理
            fileContent = null;
        }
    },

    /**
     * 解析 Excel 文件
     * @param {ArrayBuffer} arrayBuffer - Excel 文件的 ArrayBuffer
     * @param {string} fileName - 原始文件名
     * @returns {Object} SmartDocument 格式的解析结果
     */
    parseExcel: async function (arrayBuffer, fileName) {
        const startTime = performance.now();
        let contentHash = null;
        
        try {
            // 计算内容哈希
            contentHash = await this._calculateSHA256(arrayBuffer);

            // 配置解析选项（保留日期格式）
            const workbook = XLSX.read(arrayBuffer, { 
                type: 'array',
                cellDates: true,
                cellNF: true, // 保留数字格式
                cellText: true, // 强制使用格式化文本
                cellFormula: false // 不解析公式，避免注入风险
            });

            const sheetNames = workbook.SheetNames;
            if (sheetNames.length === 0) {
                throw new Error('Excel 文件不包含任何工作表');
            }

            const firstSheet = workbook.Sheets[sheetNames[0]];
            const jsonData = XLSX.utils.sheet_to_json(firstSheet, { 
                header: 1,
                defval: '',
                raw: false // 使用格式化后的文本
            });

            if (jsonData.length === 0) {
                throw new Error('第一个工作表为空');
            }

            // 处理表头（清理并去重）
            let headers = jsonData[0].map((h, i) => 
                this._cleanHeader(h?.toString() || `Column${i + 1}`)
            );
            
            // 处理重复表头
            headers = this._deduplicateHeaders(headers);

            const dataRows = jsonData.slice(1);
            const rowCount = dataRows.length;

            // 质量评估
            const quality = this._assessQuality({ headers, rowCount, data: dataRows }, 'excel', fileName);
            
            // 数据清洗与采样
            const cleanedRows = dataRows.map(row => 
                row.map(cell => this._detectMaliciousContent(cell))
            );
            const sampledData = this._smartSampleTable(cleanedRows, 1000, headers);

            // 生成 CSV 表示
            const csvRepresentation = this._convertToCSV(sampledData, headers);

            const totalTime = performance.now() - startTime;

            return {
                success: true,
                smartDocument: {
                    fileId: this._generateUUID(),
                    fileName: fileName,
                    mimeType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
                    sizeBytes: arrayBuffer.byteLength,
                    contentHash: contentHash,
                    createdAt: new Date().toISOString(),
                    parsedAt: new Date().toISOString(),
                    content: {
                        contentType: 'table',
                        csvRepresentation: csvRepresentation,
                        headers: headers,
                        rowCount: rowCount,
                        columnCount: headers.length,
                        summary: this._generateTableSummary(rowCount, headers, 'excel', sheetNames),
                        stats: null,
                        sheetNames: sheetNames
                    },
                    chunks: [
                        {
                            id: this._generateUUID(),
                            type: 2, // Table
                            content: csvRepresentation,
                            structuredData: JSON.stringify(sampledData),
                            metadata: {
                                rowCount: rowCount,
                                columnCount: headers.length,
                                sheetNames: sheetNames
                            }
                        }
                    ],
                    parseInfo: {
                        strategy: 'client-sheetjs',
                        processingTimeMs: Math.round(totalTime),
                        costUSD: 0,
                        tokensUsed: 0,
                        modelUsed: null,
                        qualityScore: quality.score,
                        warnings: quality.warnings,
                        parsedBy: 'client'
                    }
                }
            };
        } catch (error) {
            return {
                success: false,
                errorMessage: error.message,
                fallbackRecommendation: true
            };
        } finally {
            // 内存清理
            arrayBuffer = null;
        }
    },

    /**
     * 解析纯文本文件 (TXT/MD)
     * @param {string} fileContent - 文本内容
     * @param {string} fileName - 原始文件名
     * @returns {Object} SmartDocument 格式的解析结果
     */
    parseText: async function (fileContent, fileName) {
        const startTime = performance.now();
        let contentHash = null;
        
        try {
            contentHash = await this._calculateSHA256(fileContent);
            const ext = fileName.split('.').pop()?.toLowerCase() || 'txt';
            const format = ext === 'md' ? 'markdown' : 'plain';

            // 提取 Markdown 标题结构（正确计算 EndLine）
            const sections = this._extractMarkdownSections(fileContent);
            
            const totalTime = performance.now() - startTime;

            return {
                success: true,
                smartDocument: {
                    fileId: this._generateUUID(),
                    fileName: fileName,
                    mimeType: format === 'markdown' ? 'text/markdown' : 'text/plain',
                    sizeBytes: new Blob([fileContent]).size,
                    contentHash: contentHash,
                    createdAt: new Date().toISOString(),
                    parsedAt: new Date().toISOString(),
                    content: {
                        contentType: 'text',
                        text: fileContent,
                        format: format,
                        sections: sections.length > 0 ? sections : null,
                        pageCount: 1,
                        hasTables: fileContent.includes('| ') && fileContent.includes('---'),
                        hasImages: fileContent.includes('![')
                    },
                    chunks: sections.length > 0 ? sections.map(sec => ({
                        id: this._generateUUID(),
                        type: 0, // Text
                        content: this._getSectionContent(fileContent, sec.startLine, sec.endLine),
                        structuredData: null,
                        metadata: {
                            sectionTitle: sec.title,
                            level: sec.level
                        }
                    })) : [
                        {
                            id: this._generateUUID(),
                            type: 0, // Text
                            content: fileContent,
                            structuredData: null,
                            metadata: {
                                pageNum: 1
                            }
                        }
                    ],
                    parseInfo: {
                        strategy: 'client-text',
                        processingTimeMs: Math.round(totalTime),
                        costUSD: 0,
                        tokensUsed: 0,
                        modelUsed: null,
                        qualityScore: sections ? 1.0 : 0.9, // MD比纯文本质量分略高
                        warnings: [],
                        parsedBy: 'client'
                    }
                }
            };
        } catch (error) {
            return {
                success: false,
                errorMessage: error.message
            };
        } finally {
            fileContent = null;
        }
    },

    /**
     * 读取文件为 Base64 (供后端解析使用)
     * @param {File} file - 文件对象
     * @returns {Promise<Object>} 包含 base64Content 的对象
     */
    readAsBase64: async function (file) {
        return new Promise((resolve, reject) => {
            const reader = new FileReader();
            reader.onload = () => {
                try {
                    const base64 = reader.result.split(',')[1];
                    resolve({
                        success: true,
                        fileName: file.name,
                        mimeType: file.type || 'application/octet-stream',
                        base64Content: base64,
                        sizeBytes: file.size
                    });
                } catch (error) {
                    reject({ success: false, errorMessage: error.message });
                }
            };
            reader.onerror = () => reject({ 
                success: false, 
                errorMessage: reader.error?.message || '文件读取失败' 
            });
            reader.readAsDataURL(file);
        });
    },

    /**
     * 读取文件为文本
     * @param {File} file - 文件对象
     * @returns {Promise<string>} 文件文本内容
     */
    readAsText: function (file) {
        return new Promise((resolve, reject) => {
            const reader = new FileReader();
            reader.onload = () => resolve(reader.result);
            reader.onerror = () => reject(reader.error?.message);
            reader.readAsText(file);
        });
    },

    /**
     * 读取文件为 ArrayBuffer
     * @param {File} file - 文件对象
     * @returns {Promise<ArrayBuffer>} 文件 ArrayBuffer
     */
    readAsArrayBuffer: function (file) {
        return new Promise((resolve, reject) => {
            const reader = new FileReader();
            reader.onload = () => resolve(reader.result);
            reader.onerror = () => reject(reader.error?.message);
            reader.readAsArrayBuffer(file);
        });
    },

    // ========== 核心辅助方法 ==========

    /**
     * 计算 SHA256 哈希（用于 ContentHash）
     */
    _calculateSHA256: async function (content) {
        const encoder = new TextEncoder();
        const data = content instanceof ArrayBuffer ? content : encoder.encode(content);
        
        try {
            const hashBuffer = await crypto.subtle.digest('SHA-256', data);
            const hashArray = Array.from(new Uint8Array(hashBuffer));
            return hashArray.map(b => b.toString(16).padStart(2, '0')).join('');
        } catch (error) {
            console.warn('SHA256 计算失败，使用随机值代替:', error);
            return 'hash_' + Math.random().toString(36).substring(2);
        }
    },

    /**
     * 清理表头名称
     */
    _cleanHeader: function (header) {
        if (!header) return 'Column';
        return header
            .toString()
            .trim()
            .replace(/\s+/g, '_')
            .replace(/[^\w\u4e00-\u9fa5]/g, '') // 保留中文
            || 'Column';
    },

    /**
     * 去重表头（处理重复列名）
     */
    _deduplicateHeaders: function (headers) {
        const seen = new Map();
        return headers.map(header => {
            if (seen.has(header)) {
                const count = seen.get(header);
                seen.set(header, count + 1);
                return `${header}_${count + 1}`;
            } else {
                seen.set(header, 1);
                return header;
            }
        });
    },

    /**
     * 智能采样表格数据（前20%+后20%+随机60%）
     */
    _smartSampleTable: function (data, maxRows, headers = null) {
        if (data.length <= maxRows) return data;
        
        const frontCount = Math.floor(maxRows * 0.2);
        const backCount = Math.floor(maxRows * 0.2);
        const randomCount = maxRows - frontCount - backCount;
        
        const sampled = [];
        
        // 前 N 行
        sampled.push(...data.slice(0, frontCount));
        
        // 随机中间部分
        const middleStart = frontCount;
        const middleEnd = data.length - backCount;
        const availableMiddle = middleEnd - middleStart;
        
        if (availableMiddle > 0) {
            const randomIndices = new Set();
            while (randomIndices.size < Math.min(randomCount, availableMiddle)) {
                randomIndices.add(middleStart + Math.floor(Math.random() * availableMiddle));
            }
            [...randomIndices].sort((a, b) => a - b).forEach(idx => {
                sampled.push(data[idx]);
            });
        }
        
        // 后 N 行
        sampled.push(...data.slice(-backCount));
        
        return sampled;
    },

    /**
     * 将二维数组转换为 CSV 字符串
     */
    _convertToCSV: function (data, headers) {
        const lines = [headers.join(',')];
        data.forEach(row => {
            const cells = row.map(cell => {
                const val = cell?.toString() || '';
                if (val.includes(',') || val.includes('"') || val.includes('\n')) {
                    return `"${val.replace(/"/g, '""')}"`;
                }
                return val;
            });
            lines.push(cells.join(','));
        });
        return lines.join('\n');
    },

    /**
     * 质量评估（动态计算 QualityScore）
     */
    _assessQuality: function (parseResult, fileType, fileName) {
        const warnings = [];
        let score = 1.0;

        // 规则1: 空数据检测
        if (parseResult.rowCount === 0) {
            score = 0.3;
            warnings.push("表格为空或解析失败");
        }
        // 规则2: 列数异常（如CSV通常<100列）
        else if (parseResult.headers && parseResult.headers.length > 100) {
            score = 0.6;
            warnings.push("列数异常（超过100列），可能解析错误");
        }
        // 规则3: 表头缺失
        else if (!parseResult.headers || parseResult.headers.length === 0) {
            score = 0.5;
            warnings.push("未识别到有效表头");
        }
        // 规则4: 特殊字符检测（Excel 公式注入）
        else if (parseResult.data && parseResult.data.some(row => 
            Object.values(row).some(val => 
                typeof val === 'string' && val.match(/^[=+\-@]/)
            )
        )) {
            score = 0.8;
            warnings.push("检测到潜在的公式注入内容，已清理");
        }
        // 规则5: 数据类型混乱
        else if (fileType === 'csv' && parseResult.errors?.length > 0) {
            score = 0.7;
            warnings.push(`解析时发生 ${parseResult.errors.length} 个错误`);
        }

        return { score: Math.max(0.0, Math.min(1.0, score)), warnings };
    },

    /**
     * 生成表格摘要
     */
    _generateTableSummary: function (rowCount, headers, fileType, sheetNames = null) {
        let summary = `表格包含 ${rowCount} 行数据，共 ${headers.length} 列。`;
        
        // 截断声明
        if (rowCount > 1000) {
            summary += `（显示前1000行采样数据）`;
        }
        
        // 列名预览（前5列）
        summary += `\n列名：${headers.slice(0, 5).join(', ')}${headers.length > 5 ? '...' : ''}`;
        
        // Excel 多工作表提示
        if (sheetNames && sheetNames.length > 1) {
            summary += `\n工作表列表：${sheetNames.slice(0, 3).join(', ')}${sheetNames.length > 3 ? '...' : ''}`;
        }
        
        return summary;
    },

    /**
     * 提取 Markdown 章节结构（正确计算 EndLine）
     */
    _extractMarkdownSections: function (content) {
        const sections = [];
        const lines = content.split('\n');
        
        let currentSection = null;
        
        lines.forEach((line, index) => {
            const match = line.match(/^(#{1,6})\s+(.+)$/);
            if (match) {
                // 结束上一个章节
                if (currentSection) {
                    currentSection.endLine = index; // 当前章节在下一章节前结束
                }
                
                // 开始新章节
                currentSection = {
                    level: match[1].length,
                    title: match[2].trim(),
                    startLine: index + 1,
                    endLine: lines.length // 默认为文档末尾
                };
                sections.push(currentSection);
            }
        });
        
        return sections;
    },

    /**
     * 根据行号提取章节内容
     */
    _getSectionContent: function (content, startLine, endLine) {
        const lines = content.split('\n');
        // slice end index is exclusive, so use endLine directly (assuming 1-based passed in, converted to 0-based index)
        // startLine is 1-based index (from previous logic)
        return lines.slice(startLine - 1, endLine).join('\n').trim();
    },

    /**
     * 恶意内容检测（防注入）
     */
    _detectMaliciousContent: function (value) {
        if (typeof value === 'string') {
            // Excel 公式注入检测
            if (value.match(/^[=+\-@]/) && value.length > 1) {
                return `[安全清理] ${value}`;
            }
            // 超长内容截断（防止DoS）
            if (value.length > 10000) {
                return value.substring(0, 10000) + '...[截断]';
            }
        }
        return value;
    },

    /**
     * 生成 UUID
     */
    _generateUUID: function () {
        return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
            const r = Math.random() * 16 | 0;
            const v = c === 'x' ? r : (r & 0x3 | 0x8);
            return v.toString(16);
        });
    }
};

console.log('[DevNexus] FileParser 模块已加载 v1.0.0');

// 全局错误捕获
window.addEventListener('error', function (event) {
    if (event.filename && event.filename.includes('fileParser.js')) {
        console.error('DevNexusFileParser 全局错误:', event.error);
    }
});