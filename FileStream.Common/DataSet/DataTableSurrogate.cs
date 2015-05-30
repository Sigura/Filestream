using System;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace FileStream.Common
{
    [Serializable]
    internal class DataTableSurrogate
    {
        //DataTable properties
        private readonly bool _caseSensitive;

        private readonly Hashtable _colErrors = new Hashtable();
        //Keep a map between the row index and the Arraylist of columns that are in error and the error strings.

        //Columns
        private readonly DataColumnSurrogate[] _dataColumnSurrogates;
        private readonly string _displayExpression;

        //Constraints

        //ExtendedProperties
        private readonly Hashtable _extendedProperties;
        private readonly CultureInfo _locale;
        private readonly int _minimumCapacity;
        private readonly string _namespace;
        private readonly string _prefix;

        //Rows

        private readonly object[][] _records;
        //As many object[] as there are number of columns. Always send 2 records for 1 row. TradeOff between memory vs. performance. Time intensive to find which records are modified.

        private readonly Hashtable _rowErrors = new Hashtable(); //Keep a map between the row index and the row error

        private readonly BitArray _rowStates;
        //The 4 rowstates[Unchanged, Added, Modified, Deleted] are represented with 2 bits. The length of the BitArray will be twice the size of the number of rows.

        private readonly string _tableName;

        private readonly ArrayList _uniqueConstraints;
        //An ArrayList of unique constraints : [constraintName]->[columnIndexes]->[IsPrimaryKey]->[extendedProperties]

        /*
            Constructs a DataTableSurrogate from a DataTable.
        */

        public DataTableSurrogate(DataTable dt)
        {
            if (dt == null)
            {
                throw new ArgumentNullException(@"dt");
            }

            _tableName = dt.TableName;
            _namespace = dt.Namespace;
            _prefix = dt.Prefix;
            _caseSensitive = dt.CaseSensitive;
            _locale = dt.Locale;
            _displayExpression = dt.DisplayExpression;
            _minimumCapacity = dt.MinimumCapacity;

            //Columns
            _dataColumnSurrogates = new DataColumnSurrogate[dt.Columns.Count];
            for (var i = 0; i < dt.Columns.Count; i++)
            {
                _dataColumnSurrogates[i] = new DataColumnSurrogate(dt.Columns[i]);
            }

            //Constraints
            _uniqueConstraints = GetUniqueConstraints(dt);

            //ExtendedProperties
            _extendedProperties = new Hashtable();
            if (dt.ExtendedProperties.Keys.Count > 0)
            {
                foreach (var propertyKey in dt.ExtendedProperties.Keys)
                {
                    _extendedProperties.Add(propertyKey, dt.ExtendedProperties[propertyKey]);
                }
            }

            //Rows
            if (dt.Rows.Count > 0)
            {
                _rowStates = new BitArray(dt.Rows.Count << 1);
                _records = new object[dt.Columns.Count][];
                for (var i = 0; i < dt.Columns.Count; i++)
                {
                    _records[i] = new object[dt.Rows.Count << 1];
                }
                for (var i = 0; i < dt.Rows.Count; i++)
                {
                    GetRecords(dt.Rows[i], i << 1);
                }
            }
        }

        /*
            Constructs a DataTable from DataTableSurrogate. 
        */

        public DataTable ConvertToDataTable()
        {
            var dt = new DataTable();
            ReadSchemaIntoDataTable(dt);
            ReadDataIntoDataTable(dt);
            return dt;
        }

        /*
            Reads the schema into the datatable from DataTableSurrogate. 
        */

        public void ReadSchemaIntoDataTable(DataTable dt)
        {
            if (dt == null)
            {
                throw new ArgumentNullException(@"dt");
            }

            dt.TableName = _tableName;
            dt.Namespace = _namespace;
            dt.Prefix = _prefix;
            dt.CaseSensitive = _caseSensitive;
            dt.Locale = _locale;
            dt.DisplayExpression = _displayExpression;
            dt.MinimumCapacity = _minimumCapacity;

            Debug.Assert(_dataColumnSurrogates != null);

            foreach (var dc in _dataColumnSurrogates.Select(dataColumnSurrogate => dataColumnSurrogate.ConvertToDataColumn()))
            {
                dt.Columns.Add(dc);
            }

            //UniqueConstraints
            SetUniqueConstraints(dt, _uniqueConstraints);

            //Extended properties
            Debug.Assert(_extendedProperties != null);
            if (_extendedProperties.Keys.Count > 0)
            {
                foreach (var propertyKey in _extendedProperties.Keys)
                {
                    dt.ExtendedProperties.Add(propertyKey, _extendedProperties[propertyKey]);
                }
            }
        }

        /*
            Reads the data into a DataTable from DataTableSurrogate. 
        */

        public void ReadDataIntoDataTable(DataTable dt)
        {
            ReadDataIntoDataTable(dt, true);
        }

        /*
            Copies the rows into a DataTable from DataTableSurrogate. 
        */

        internal void ReadDataIntoDataTable(DataTable dt, bool suppressSchema)
        {
            if (dt == null)
            {
                throw new ArgumentNullException("dt");
            }
            Debug.Assert(IsSchemaIdentical(dt));

            //Suppress read-only and constraint rules while loading the data.
            ArrayList readOnlyList = null;
            ArrayList constraintRulesList = null;
            if (suppressSchema)
            {
                readOnlyList = SuppressReadOnly(dt);
                constraintRulesList = SuppressConstraintRules(dt);
            }

            //Read the rows
            if (_records != null && dt.Columns.Count > 0)
            {
                Debug.Assert(_records.Length > 0);
                var rowCount = _records[0].Length >> 1;
                for (var i = 0; i < rowCount; i++)
                {
                    ConvertToDataRow(dt, i << 1);
                }
            }

            //Reset read-only column and constraint rules back after loading the data.
            if (suppressSchema)
            {
                ResetReadOnly(dt, readOnlyList);
                ResetConstraintRules(dt, constraintRulesList);
            }
        }

        /*
            Gets unique constraints availabe on the datatable.
            ***Serialized unique constraints format : [constraintName]->[columnIndexes]->[IsPrimaryKey]->[extendedProperties]***
        */

        private ArrayList GetUniqueConstraints(DataTable dt)
        {
            Debug.Assert(dt != null);

            var constraintList = new ArrayList();
            for (var i = 0; i < dt.Constraints.Count; i++)
            {
                var c = dt.Constraints[i];
                var uc = c as UniqueConstraint;
                if (uc != null)
                {
                    var constraintName = c.ConstraintName;
                    var colInfo = new int[uc.Columns.Length];
                    for (var j = 0; j < colInfo.Length; j++)
                    {
                        colInfo[j] = uc.Columns[j].Ordinal;
                    }

                    var list = new ArrayList { constraintName, colInfo, uc.IsPrimaryKey };
                    var extendedProperties = new Hashtable();
                    if (uc.ExtendedProperties.Keys.Count > 0)
                    {
                        foreach (var propertyKey in uc.ExtendedProperties.Keys)
                        {
                            extendedProperties.Add(propertyKey, uc.ExtendedProperties[propertyKey]);
                        }
                    }
                    list.Add(extendedProperties);

                    constraintList.Add(list);
                }
            }
            return constraintList;
        }

        /*
            Adds unique constraints to the table. The arraylist contains the serialized format of the unique constraints.
            ***Deserialize the unique constraints format : [constraintName]->[columnIndexes]->[IsPrimaryKey]->[extendedProperties]***
        */

        private void SetUniqueConstraints(DataTable dt, ArrayList constraintList)
        {
            Debug.Assert(dt != null);
            Debug.Assert(constraintList != null);

            foreach (ArrayList list in constraintList)
            {
                Debug.Assert(list.Count == 4);
                var constraintName = (string)list[0];
                var keyColumnIndexes = (int[])list[1];
                var isPrimaryKey = (bool)list[2];
                var extendedProperties = (Hashtable)list[3];

                var keyColumns = new DataColumn[keyColumnIndexes.Length];
                for (var i = 0; i < keyColumnIndexes.Length; i++)
                {
                    Debug.Assert(dt.Columns.Count > keyColumnIndexes[i]);
                    keyColumns[i] = dt.Columns[keyColumnIndexes[i]];
                }
                //Create the constraint.
                var uc = new UniqueConstraint(constraintName, keyColumns, isPrimaryKey);
                //Extended Properties.
                Debug.Assert(extendedProperties != null);
                if (extendedProperties.Keys.Count > 0)
                {
                    foreach (var propertyKey in extendedProperties.Keys)
                    {
                        uc.ExtendedProperties.Add(propertyKey, extendedProperties[propertyKey]);
                    }
                }
                dt.Constraints.Add(uc);
            }
        }

        /*
            Sets  expression on the columns.
        */

        internal void SetColumnExpressions(DataTable dt)
        {
            Debug.Assert(dt != null);

            Debug.Assert(_dataColumnSurrogates != null);
            Debug.Assert(dt.Columns.Count == _dataColumnSurrogates.Length);
            for (var i = 0; i < dt.Columns.Count; i++)
            {
                var dc = dt.Columns[i];
                var dataColumnSurrogate = _dataColumnSurrogates[i];
                dataColumnSurrogate.SetColumnExpression(dc);
            }
        }

        /*
            Gets the records from the rows.
        */

        private void GetRecords(DataRow row, int bitIndex)
        {
            Debug.Assert(row != null);

            ConvertToSurrogateRowState(row.RowState, bitIndex);
            ConvertToSurrogateRecords(row, bitIndex);
            ConvertToSurrogateRowError(row, bitIndex >> 1);
        }

        /*
            Constructs the row, rowError and columnErrors.
        */

        public DataRow ConvertToDataRow(DataTable dt, int bitIndex)
        {
            var rowState = ConvertToRowState(bitIndex);
            var row = ConstructRow(dt, rowState, bitIndex);
            ConvertToRowError(row, bitIndex >> 1);
            return row;
        }

        /*
            Sets the two bits in the bitArray to represent the DataRowState.
            The 4 rowstates[Unchanged, Added, Modified, Deleted] are represented with 2 bits. The length of the BitArray will be twice the size of the number of rows.
            Serialozed rowstate format : [00]->UnChanged, [01]->Added, [10]->Modified, [11]->Deleted.
        */

        private void ConvertToSurrogateRowState(DataRowState rowState, int bitIndex)
        {
            Debug.Assert(_rowStates != null);
            Debug.Assert(_rowStates.Length > bitIndex);

            switch (rowState)
            {
                case DataRowState.Unchanged:
                    _rowStates[bitIndex] = false;
                    _rowStates[bitIndex + 1] = false;
                    break;
                case DataRowState.Added:
                    _rowStates[bitIndex] = false;
                    _rowStates[bitIndex + 1] = true;
                    break;
                case DataRowState.Modified:
                    _rowStates[bitIndex] = true;
                    _rowStates[bitIndex + 1] = false;
                    break;
                case DataRowState.Deleted:
                    _rowStates[bitIndex] = true;
                    _rowStates[bitIndex + 1] = true;
                    break;
                default:
                    throw new InvalidEnumArgumentException(String.Format("Unrecognized row state {0}", rowState));
            }
        }

        /*
            Constructs the RowState from the two bits in the bitarray.
            Deserialize rowstate format : [00]->UnChanged, [01]->Added, [10]->Modified, [11]->Deleted.
        */

        private DataRowState ConvertToRowState(int bitIndex)
        {
            Debug.Assert(_rowStates != null);
            Debug.Assert(_rowStates.Length > bitIndex);

            var b1 = _rowStates[bitIndex];
            var b2 = _rowStates[bitIndex + 1];

            if (!b1 && !b2)
            {
                return DataRowState.Unchanged;
            }
            if (!b1)
            {
                return DataRowState.Added;
            }
            return !b2 ? DataRowState.Modified : DataRowState.Deleted;

            //throw new ArgumentException("Unrecognized bitpattern");
        }

        /*
            Constructs surrogate records from the DataRow.
        */

        private void ConvertToSurrogateRecords(DataRow row, int bitIndex)
        {
            Debug.Assert(row != null);
            Debug.Assert(_records != null);

            var colCount = row.Table.Columns.Count;
            var rowState = row.RowState;

            Debug.Assert(_records.Length == colCount);
            if (rowState != DataRowState.Added)
            {
                //Unchanged, modified, deleted     
                for (var i = 0; i < colCount; i++)
                {
                    Debug.Assert(_records[i].Length > bitIndex);
                    _records[i][bitIndex] = row[i, DataRowVersion.Original];
                }
            }

            if (rowState != DataRowState.Unchanged && rowState != DataRowState.Deleted)
            {
                //Added, modified state
                for (var i = 0; i < colCount; i++)
                {
                    Debug.Assert(_records[i].Length > bitIndex + 1);
                    _records[i][bitIndex + 1] = row[i, DataRowVersion.Current];
                }
            }
        }

        /*
            Constructs a DataRow from records[original and current] and adds the row to the DataTable rows collection.
        */

        private DataRow ConstructRow(DataTable dt, DataRowState rowState, int bitIndex)
        {
            Debug.Assert(dt != null);
            Debug.Assert(_records != null);

            var row = dt.NewRow();
            var colCount = dt.Columns.Count;

            Debug.Assert(_records.Length == colCount);
            switch (rowState)
            {
                case DataRowState.Unchanged:
                    for (var i = 0; i < colCount; i++)
                    {
                        Debug.Assert(_records[i].Length > bitIndex);
                        row[i] = _records[i][bitIndex]; //Original Record
                    }
                    dt.Rows.Add(row);
                    row.AcceptChanges();
                    break;
                case DataRowState.Added:
                    for (var i = 0; i < colCount; i++)
                    {
                        Debug.Assert(_records[i].Length > bitIndex + 1);
                        row[i] = _records[i][bitIndex + 1]; //Current Record
                    }
                    dt.Rows.Add(row);
                    break;
                case DataRowState.Modified:
                    for (var i = 0; i < colCount; i++)
                    {
                        Debug.Assert(_records[i].Length > bitIndex);
                        row[i] = _records[i][bitIndex]; //Original Record
                    }
                    dt.Rows.Add(row);
                    row.AcceptChanges();
                    row.BeginEdit();
                    for (var i = 0; i < colCount; i++)
                    {
                        Debug.Assert(_records[i].Length > bitIndex + 1);
                        row[i] = _records[i][bitIndex + 1]; //Current Record
                    }
                    row.EndEdit();
                    break;
                case DataRowState.Deleted:
                    for (var i = 0; i < colCount; i++)
                    {
                        Debug.Assert(_records[i].Length > bitIndex);
                        row[i] = _records[i][bitIndex]; //Original Record
                    }
                    dt.Rows.Add(row);
                    row.AcceptChanges();
                    row.Delete();
                    break;
                default:
                    throw new InvalidEnumArgumentException(String.Format("Unrecognized row state {0}", rowState));
            }
            return row;
        }

        /*
            Constructs the surrogate rowerror, columnsInError and columnErrors.
        */

        private void ConvertToSurrogateRowError(DataRow row, int rowIndex)
        {
            Debug.Assert(row != null);
            Debug.Assert(_rowErrors != null);
            Debug.Assert(_colErrors != null);

            if (row.HasErrors)
            {
                _rowErrors.Add(rowIndex, row.RowError);
                var dcArr = row.GetColumnsInError();
                if (dcArr.Length > 0)
                {
                    var columnsInError = new int[dcArr.Length];
                    var columnErrors = new string[dcArr.Length];
                    for (var i = 0; i < dcArr.Length; i++)
                    {
                        columnsInError[i] = dcArr[i].Ordinal;
                        columnErrors[i] = row.GetColumnError(dcArr[i]);
                    }
                    var list = new ArrayList { columnsInError, columnErrors };
                    _colErrors.Add(rowIndex, list);
                }
            }
        }

        /*
            Set the row and columns in error.
        */

        private void ConvertToRowError(DataRow row, int rowIndex)
        {
            Debug.Assert(row != null);
            Debug.Assert(_rowErrors != null);
            Debug.Assert(_colErrors != null);

            if (_rowErrors.ContainsKey(rowIndex))
            {
                row.RowError = (string)_rowErrors[rowIndex];
            }
            if (_colErrors.ContainsKey(rowIndex))
            {
                var list = (ArrayList)_colErrors[rowIndex];
                var columnsInError = (int[])list[0];
                var columnErrors = (string[])list[1];
                Debug.Assert(columnsInError.Length == columnErrors.Length);
                for (var i = 0; i < columnsInError.Length; i++)
                {
                    row.SetColumnError(columnsInError[i], columnErrors[i]);
                }
            }
        }

        /*
            Suppress the read-only property and returns an arraylist of read-only columns.
        */

        private ArrayList SuppressReadOnly(DataTable dt)
        {
            Debug.Assert(dt != null);
            var readOnlyList = new ArrayList();
            for (var j = 0; j < dt.Columns.Count; j++)
            {
                if (dt.Columns[j].Expression == String.Empty && dt.Columns[j].ReadOnly)
                {
                    readOnlyList.Add(j);
                }
            }
            return readOnlyList;
        }

        /*
            Suppress the foreign key constraint rules and returns an arraylist of the existing foreignkey constraint rules.
        */

        private static ArrayList SuppressConstraintRules(DataTable dt)
        {
            Debug.Assert(dt != null);
            var constraintRulesList = new ArrayList();
            var ds = dt.DataSet;
            if (ds != null)
            {
                for (var i = 0; i < ds.Tables.Count; i++)
                {
                    var dtChild = ds.Tables[i];
                    for (var j = 0; j < dtChild.Constraints.Count; j++)
                    {
                        var c = dtChild.Constraints[j];
                        var fk = c as ForeignKeyConstraint;
                        if (fk == null) continue;
                        if (fk.RelatedTable != dt) continue;
                        var list = new ArrayList
                        {
                            new[] {i, j},
                            new[] {(int) fk.AcceptRejectRule, (int) fk.UpdateRule, (int) fk.DeleteRule}
                        };
                        constraintRulesList.Add(list);

                        fk.AcceptRejectRule = AcceptRejectRule.None;
                        fk.UpdateRule = Rule.None;
                        fk.DeleteRule = Rule.None;
                    }
                }
            }
            return constraintRulesList;
        }

        /*
            Resets the read-only columns on the datatable based on the input readOnly list.
        */

        private void ResetReadOnly(DataTable dt, ArrayList readOnlyList)
        {
            Debug.Assert(dt != null);
            Debug.Assert(readOnlyList != null);

            //var ds = dt.DataSet;
            foreach (var columnIndex in readOnlyList.Cast<int>())
            {
                Debug.Assert(dt.Columns.Count > columnIndex);
                dt.Columns[columnIndex].ReadOnly = true;
            }
        }

        /*
            Reset the foreignkey constraint rules on the datatable based on the input constraintRules list.
        */

        private void ResetConstraintRules(DataTable dt, ArrayList constraintRulesList)
        {
            Debug.Assert(dt != null);
            Debug.Assert(constraintRulesList != null);

            var ds = dt.DataSet;
            foreach (ArrayList list in constraintRulesList)
            {
                Debug.Assert(list.Count == 2);
                var indicesArr = (int[])list[0];
                var rules = (int[])list[1];

                Debug.Assert(indicesArr.Length == 2);
                var tableIndex = indicesArr[0];
                var constraintIndex = indicesArr[1];

                Debug.Assert(ds.Tables.Count > tableIndex);
                var dtChild = ds.Tables[tableIndex];

                Debug.Assert(dtChild.Constraints.Count > constraintIndex);
                var fk = (ForeignKeyConstraint)dtChild.Constraints[constraintIndex];

                Debug.Assert(rules.Length == 3);
                fk.AcceptRejectRule = (AcceptRejectRule)rules[0];
                fk.UpdateRule = (Rule)rules[1];
                fk.DeleteRule = (Rule)rules[2];
            }
        }

        /*
            Checks whether the datatable schema matches with the surrogate schema.
        */

        private bool IsSchemaIdentical(DataTable dt)
        {
            Debug.Assert(dt != null);

            if (dt.TableName != _tableName || dt.Namespace != _namespace)
            {
                return false;
            }

            Debug.Assert(_dataColumnSurrogates != null);
            if (dt.Columns.Count != _dataColumnSurrogates.Length)
            {
                return false;
            }
            for (var i = 0; i < dt.Columns.Count; i++)
            {
                var dc = dt.Columns[i];
                var dataColumnSurrogate = _dataColumnSurrogates[i];
                if (!dataColumnSurrogate.IsSchemaIdentical(dc))
                {
                    return false;
                }
            }
            return true;
        }
    }
}