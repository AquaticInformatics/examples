using System;

namespace PointZilla
{
    public class Field
    {
        public static Field Parse(string text, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ExpectedException($"{fieldName}: You must specify a positive integer or a named column for a field");

            if (int.TryParse(text, out var index))
            {
                if (index > 0)
                    return new Field(fieldName, index);

                throw new Exception($"{fieldName}: {text} is an invalid index. Use a positive integer or a named column.");
            }

            return new Field(fieldName, text);
        }

        public int ColumnIndex { get; set; }
        public string ColumnName { get; }
        public string FieldName { get; }

        public bool HasColumnName => !string.IsNullOrWhiteSpace(ColumnName);
        public bool HasColumnIndex => ColumnIndex > 0;

        private Field(string fieldName, int columnIndex)
        {
            FieldName = fieldName;
            ColumnIndex = columnIndex;
        }

        private Field(string fieldName, string columnName)
        {
            FieldName = fieldName;
            ColumnName = columnName;
        }

        public override string ToString()
        {
            return HasColumnName
                ? $"{FieldName}:'{ColumnName}'"
                : $"{FieldName}:#{ColumnIndex}";
        }
    }
}
