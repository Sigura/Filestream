using System;
using System.Collections;
using System.Data;
using System.Diagnostics;

namespace FileStream.Common
{
    [Serializable]
    internal class DataColumnSurrogate
    {
        private readonly bool _allowNull;
        private readonly bool _autoIncrement;
        private readonly long _autoIncrementSeed;
        private readonly long _autoIncrementStep;
        private readonly string _caption;
        private readonly MappingType _columnMapping;
        private readonly string _columnName;
        private readonly Type _dataType;
        private readonly object _defaultValue;
        private readonly string _expression;

        //ExtendedProperties
        private readonly Hashtable _extendedProperties;
        private readonly int _maxLength;
        private readonly string _namespace;
        private readonly string _prefix;
        private readonly bool _readOnly;

        /*
            Constructs a DataColumnSurrogate from a DataColumn.
        */

        public DataColumnSurrogate(DataColumn dc)
        {
            if (dc == null)
            {
                throw new ArgumentNullException(@"The datacolumn parameter is null");
            }
            _columnName = dc.ColumnName;
            _namespace = dc.Namespace;
            _dataType = dc.DataType;
            _prefix = dc.Prefix;
            _columnMapping = dc.ColumnMapping;
            _allowNull = dc.AllowDBNull;
            _autoIncrement = dc.AutoIncrement;
            _autoIncrementStep = dc.AutoIncrementStep;
            _autoIncrementSeed = dc.AutoIncrementSeed;
            _caption = dc.Caption;
            _defaultValue = dc.DefaultValue;
            _readOnly = dc.ReadOnly;
            _maxLength = dc.MaxLength;
            _expression = dc.Expression;

            //ExtendedProperties
            _extendedProperties = new Hashtable();
            if (dc.ExtendedProperties.Keys.Count > 0)
            {
                foreach (var propertyKey in dc.ExtendedProperties.Keys)
                {
                    _extendedProperties.Add(propertyKey, dc.ExtendedProperties[propertyKey]);
                }
            }
        }

        /*
            Constructs a DataColumn from DataColumnSurrogate.
        */

        public DataColumn ConvertToDataColumn()
        {
            var dc = new DataColumn
            {
                ColumnName = _columnName,
                Namespace = _namespace,
                DataType = _dataType,
                Prefix = _prefix,
                ColumnMapping = _columnMapping,
                AllowDBNull = _allowNull,
                AutoIncrement = _autoIncrement,
                AutoIncrementStep = _autoIncrementStep,
                AutoIncrementSeed = _autoIncrementSeed,
                Caption = _caption,
                DefaultValue = _defaultValue,
                ReadOnly = _readOnly,
                MaxLength = _maxLength
            };
            //dc.Expression = _expression;

            //Extended properties
            Debug.Assert(_extendedProperties != null);
            if (_extendedProperties.Keys.Count > 0)
            {
                foreach (var propertyKey in _extendedProperties.Keys)
                {
                    dc.ExtendedProperties.Add(propertyKey, _extendedProperties[propertyKey]);
                }
            }
            return dc;
        }

        /*
            Set expression on the DataColumn.
        */

        internal void SetColumnExpression(DataColumn dc)
        {
            Debug.Assert(dc != null);

            if (_expression != null && !_expression.Equals(String.Empty))
            {
                dc.Expression = _expression;
            }
        }

        /*
            Checks whether the column schema is identical. Marked internal as the DataTableSurrogate objects needs to have access to this object.
            Note: ReadOnly is not checked here as we suppress readonly when reading data.
        */

        internal bool IsSchemaIdentical(DataColumn dc)
        {
            Debug.Assert(dc != null);
            return (dc.ColumnName == _columnName) && (dc.Namespace == _namespace) && (dc.DataType == _dataType) && (dc.Prefix == _prefix) && (dc.ColumnMapping == _columnMapping) && (dc.ColumnMapping == _columnMapping) && (dc.AllowDBNull == _allowNull) && (dc.AutoIncrement == _autoIncrement) && (dc.AutoIncrementStep == _autoIncrementStep) && (dc.AutoIncrementSeed == _autoIncrementSeed) && (dc.Caption == _caption) && (AreDefaultValuesEqual(dc.DefaultValue, _defaultValue)) && (dc.MaxLength == _maxLength) && (dc.Expression == _expression);
        }

        /*
            Checks whether the default boxed objects are equal.
        */

        internal static bool AreDefaultValuesEqual(object o1, object o2)
        {
            if (o1 == null && o2 == null)
            {
                return true;
            }
            if (o1 == null || o2 == null)
            {
                return false;
            }

            return o1.Equals(o2);
        }
    }
}