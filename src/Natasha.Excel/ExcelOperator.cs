﻿using Natasha.CSharp;
using Natasha.Excel;
using NPOI.SS.UserModel;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;

public class ExcelOperator
{

    public static void ConfigWritter<TEntity>(Dictionary<string, string> mappers, params string[] ignores)
    {
        ExcelOperator<TEntity>.CreateWriteDelegate(mappers, ignores);
    }
    public static void ConfigReader<TEntity>(Dictionary<string, string> mappers)
    {
        ExcelOperator<TEntity>.CreateReadDelegate(mappers);
    }

    public static void WriteToFile<TEntity>(string filePath, IEnumerable<TEntity> entities, int sheetPage = 0)
    {
        ExcelOperator<TEntity>.WriteToFile(filePath, entities, sheetPage);
    }
    public static IEnumerable<TEntity> FileToEntities<TEntity>(string filePath, int sheetPage = 0)
    {
        return ExcelOperator<TEntity>.FileToEntities(filePath, sheetPage);
    }
}


public class ExcelOperator<TEntity>
{

    private static ImmutableDictionary<string, string> _mappers;
    private static ImmutableDictionary<string, int> _fields;
    private static Action<ISheet, IEnumerable<TEntity>> Writter;
    private static Func<ISheet, int[], IEnumerable<TEntity>> Reader;

    public static Action<ISheet, IEnumerable<TEntity>> CreateWriteDelegate(Dictionary<string, string> mappers, params string[] ignores)
    {
        _mappers = ImmutableDictionary.CreateRange(mappers);
        HashSet<string> ignorSets = new HashSet<string>(ignores);
        StringBuilder excelBody = new StringBuilder();
        StringBuilder excelHeader = new StringBuilder();
        excelHeader.AppendLine("var rowIndex = 0;");
        excelHeader.AppendLine("IRow row = arg1.CreateRow(rowIndex);");

        excelBody.AppendLine(@"foreach(var item in arg2){");
        excelBody.AppendLine($"rowIndex+=1;");
        excelBody.AppendLine($"row = arg1.CreateRow(rowIndex);");
        int column = 0;
        foreach (var item in mappers)
        {

            if (!ignorSets.Contains(item.Key))
            {

                excelBody.AppendLine($"row.CreateCell({column}).SetCellValue(item.{item.Value});");
                excelHeader.AppendLine($"row.CreateCell({column}).SetCellValue(\"{item.Key}\");");
                column += 1;

            }

        }
        excelBody.AppendLine("}");
        excelHeader.Append(excelBody);
        return Writter = NDelegate
            .UseDomain(typeof(TEntity).GetDomain())
            .Action<ISheet, IEnumerable<TEntity>>(excelHeader.ToString());

    }
    public static Func<ISheet, int[], IEnumerable<TEntity>> CreateReadDelegate(Dictionary<string, string> mappers)
    {


        //给字段排序
        int index = 0;
        var tempDict = new Dictionary<string, int>();
        foreach (var item in mappers)
        {
            tempDict[item.Value] = index;
            index += 1;
        }
        _fields = ImmutableDictionary.CreateRange(tempDict);


        StringBuilder excelBody = new StringBuilder();
        excelBody.AppendLine($"var list = new List<{typeof(TEntity).GetDevelopName()}>(arg1.LastRowNum);");
        excelBody.AppendLine(@"for(int i = 1;i<=arg1.LastRowNum;i+=1){");
        excelBody.AppendLine("var row = arg1.GetRow(i);");
        excelBody.AppendLine($"var instance = new {typeof(TEntity).GetDevelopName()}();");
        foreach (var item in _fields)
        {
            var prop = typeof(TEntity).GetProperty(item.Key);
            if (prop != null)
            {
                if (prop.PropertyType == typeof(string))
                {
                    excelBody.AppendLine($"instance.{item.Key} = row.GetCell(arg2[{item.Value}]).StringCellValue;");
                }
                else if (prop.PropertyType == typeof(DateTime))
                {
                    excelBody.AppendLine($"instance.{item.Key} = row.GetCell(arg2[{item.Value}]).DateCellValue;");
                }
                else if (prop.PropertyType == typeof(bool))
                {
                    excelBody.AppendLine($"instance.{item.Key} = row.GetCell(arg2[{item.Value}]).BooleanCellValue;");

                }
                else if (prop.PropertyType == typeof(double))
                {
                    excelBody.AppendLine($"instance.{item.Key} = row.GetCell(arg2[{item.Value}]).NumericCellValue;");
                }
                else
                {
                    excelBody.AppendLine($"instance.{item.Key} = Convert.To{prop.PropertyType.Name}(row.GetCell(arg2[{item.Value}]).NumericCellValue);");
                }
            }

        }

        excelBody.AppendLine("list.Add(instance);");
        excelBody.AppendLine("}");
        excelBody.AppendLine("return list;");
        return Reader = NDelegate
            .UseDomain(typeof(TEntity).GetDomain())
            .Func<ISheet, int[], IEnumerable<TEntity>>(excelBody.ToString());
    }


    public static void WriteToFile(string filePath, IEnumerable<TEntity> entities, int sheetPage)
    {

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
        using (var builder = new ExcelBuilder(filePath))
        {
            Writter(builder[sheetPage], entities);
            builder.Save();
        }


    }


    public static IEnumerable<TEntity> FileToEntities(string filePath, int sheetPage)
    {

        using (var builder = new ExcelBuilder(filePath))
        {

            var indexs = new int[_mappers.Count];
            var sheet = builder[sheetPage];
            var row = sheet.GetRow(0);
            for (int i = 0; i < row.LastCellNum; i += 1)
            {

                if (_mappers.TryGetValue(row.GetCell(i).StringCellValue, out var field))
                {
                    if (_fields.TryGetValue(field, out var value))
                    {
                        indexs[value] = i;
                    }
                }

            }
            return Reader(sheet, indexs);

        }

    }

}

