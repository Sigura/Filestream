using System;
using System.Collections;
using System.Data;
using System.Diagnostics;
using System.Globalization;

namespace FileStream.Common
{
    [Serializable]
    public class DataSetSurrogate
    {
        //DataSet properties
        private readonly bool _caseSensitive;
        private readonly DataTableSurrogate[] _dataTableSurrogates;
        private readonly string _datasetName;
        private readonly bool _enforceConstraints;
        private readonly Hashtable _extendedProperties;

        //ForeignKeyConstraints
        private readonly ArrayList _fkConstraints;
        //An ArrayList of foreign key constraints :  [constraintName]->[parentTableIndex, parentcolumnIndexes]->[childTableIndex, childColumnIndexes]->[AcceptRejectRule, UpdateRule, Delete]->[extendedProperties]

        private readonly CultureInfo _locale;
        private readonly string _namespace;
        private readonly string _prefix;

        //Relations
        private readonly ArrayList _relations;
        //An ArrayList of foreign key constraints : [relationName]->[parentTableIndex, parentcolumnIndexes]->[childTableIndex, childColumnIndexes]->[Nested]->[extendedProperties]

        //ExtendedProperties

        /*
            Constructs a DataSetSurrogate object from a DataSet.
        */

        public DataSetSurrogate(DataSet ds)
        {
            if (ds == null)
            {
                throw new ArgumentNullException(@"ds");
            }

            //DataSet properties
            _datasetName = ds.DataSetName;
            _namespace = ds.Namespace;
            _prefix = ds.Prefix;
            _caseSensitive = ds.CaseSensitive;
            _locale = ds.Locale;
            _enforceConstraints = ds.EnforceConstraints;

            //Tables, Columns, Rows
            _dataTableSurrogates = new DataTableSurrogate[ds.Tables.Count];
            for (var i = 0; i < ds.Tables.Count; i++)
            {
                _dataTableSurrogates[i] = new DataTableSurrogate(ds.Tables[i]);
            }

            //ForeignKeyConstraints
            _fkConstraints = GetForeignKeyConstraints(ds);

            //Relations
            _relations = GetRelations(ds);

            //ExtendedProperties
            _extendedProperties = new Hashtable();
            if (ds.ExtendedProperties.Keys.Count > 0)
            {
                foreach (var propertyKey in ds.ExtendedProperties.Keys)
                {
                    _extendedProperties.Add(propertyKey, ds.ExtendedProperties[propertyKey]);
                }
            }
        }

        /*
            Constructs a DataSet from the DataSetSurrogate object. This can be used after the user recieves a Surrogate object over the wire and wished to construct a DataSet from it.
        */

        public DataSet ConvertToDataSet()
        {
            var ds = new DataSet();
            ReadSchemaIntoDataSet(ds);
            ReadDataIntoDataSet(ds);
            return ds;
        }

        /*
            Reads the schema into the dataset from the DataSetSurrogate object.
        */

        public void ReadSchemaIntoDataSet(DataSet ds)
        {
            if (ds == null)
            {
                throw new ArgumentNullException(@"ds");
            }

            //DataSet properties
            ds.DataSetName = _datasetName;
            ds.Namespace = _namespace;
            ds.Prefix = _prefix;
            ds.CaseSensitive = _caseSensitive;
            ds.Locale = _locale;
            ds.EnforceConstraints = _enforceConstraints;

            //Tables, Columns
            Debug.Assert(_dataTableSurrogates != null);
            foreach (var dataTableSurrogate in _dataTableSurrogates)
            {
                var dt = new DataTable();
                dataTableSurrogate.ReadSchemaIntoDataTable(dt);
                ds.Tables.Add(dt);
            }

            //ForeignKeyConstraints
            SetForeignKeyConstraints(ds, _fkConstraints);

            //Relations
            SetRelations(ds, _relations);

            //Set ExpressionColumns        
            Debug.Assert(_dataTableSurrogates != null);
            Debug.Assert(ds.Tables.Count == _dataTableSurrogates.Length);
            for (var i = 0; i < ds.Tables.Count; i++)
            {
                var dt = ds.Tables[i];
                var dataTableSurrogate = _dataTableSurrogates[i];
                dataTableSurrogate.SetColumnExpressions(dt);
            }

            //ExtendedProperties
            Debug.Assert(_extendedProperties != null);
            if (_extendedProperties.Keys.Count > 0)
            {
                foreach (var propertyKey in _extendedProperties.Keys)
                {
                    ds.ExtendedProperties.Add(propertyKey, _extendedProperties[propertyKey]);
                }
            }
        }

        /*
            Reads the data into the dataset from the DataSetSurrogate object.
        */

        public void ReadDataIntoDataSet(DataSet ds)
        {
            if (ds == null)
            {
                throw new ArgumentNullException(@"ds");
            }

            //Suppress  read-only columns and constraint rules when loading the data
            var readOnlyList = SuppressReadOnly(ds);
            var constraintRulesList = SuppressConstraintRules(ds);

            //Rows
            Debug.Assert(IsSchemaIdentical(ds));
            Debug.Assert(_dataTableSurrogates != null);
            Debug.Assert(ds.Tables.Count == _dataTableSurrogates.Length);
            var enforceConstraints = ds.EnforceConstraints;
            ds.EnforceConstraints = false;
            for (var i = 0; i < ds.Tables.Count; i++)
            {
                //var dt = ds.Tables[i];
                var dataTableSurrogate = _dataTableSurrogates[i];
                dataTableSurrogate.ReadDataIntoDataTable(ds.Tables[i], false);
            }
            ds.EnforceConstraints = enforceConstraints;

            //Reset read-only columns and constraint rules back after loading the data
            ResetReadOnly(ds, readOnlyList);
            ResetConstraintRules(ds, constraintRulesList);
        }

        /*
            Gets foreignkey constraints availabe on the tables in the dataset.
            ***Serialized foreign key constraints format : [constraintName]->[parentTableIndex, parentcolumnIndexes]->[childTableIndex, childColumnIndexes]->[AcceptRejectRule, UpdateRule, Delete]->[extendedProperties]***
        */

        private ArrayList GetForeignKeyConstraints(DataSet ds)
        {
            Debug.Assert(ds != null);

            var constraintList = new ArrayList();
            for (var i = 0; i < ds.Tables.Count; i++)
            {
                var dt = ds.Tables[i];
                for (var j = 0; j < dt.Constraints.Count; j++)
                {
                    var c = dt.Constraints[j];
                    var fk = c as ForeignKeyConstraint;
                    if (fk != null)
                    {
                        var constraintName = c.ConstraintName;
                        var parentInfo = new int[fk.RelatedColumns.Length + 1];
                        parentInfo[0] = ds.Tables.IndexOf(fk.RelatedTable);
                        for (var k = 1; k < parentInfo.Length; k++)
                        {
                            parentInfo[k] = fk.RelatedColumns[k - 1].Ordinal;
                        }

                        var childInfo = new int[fk.Columns.Length + 1];
                        childInfo[0] = i; //Since the constraint is on the current table, this is the child table.
                        for (var k = 1; k < childInfo.Length; k++)
                        {
                            childInfo[k] = fk.Columns[k - 1].Ordinal;
                        }

                        var list = new ArrayList
                        {
                            constraintName,
                            parentInfo,
                            childInfo,
                            new[] {(int) fk.AcceptRejectRule, (int) fk.UpdateRule, (int) fk.DeleteRule}
                        };
                        var extendedProperties = new Hashtable();
                        if (fk.ExtendedProperties.Keys.Count > 0)
                        {
                            foreach (var propertyKey in fk.ExtendedProperties.Keys)
                            {
                                extendedProperties.Add(propertyKey, fk.ExtendedProperties[propertyKey]);
                            }
                        }
                        list.Add(extendedProperties);

                        constraintList.Add(list);
                    }
                }
            }
            return constraintList;
        }

        /*
            Adds foreignkey constraints to the tables in the dataset. The arraylist contains the serialized format of the foreignkey constraints.
            ***Deserialize the foreign key constraints format : [constraintName]->[parentTableIndex, parentcolumnIndexes]->[childTableIndex, childColumnIndexes]->[AcceptRejectRule, UpdateRule, Delete]->[extendedProperties]***
        */

        private void SetForeignKeyConstraints(DataSet ds, ArrayList constraintList)
        {
            Debug.Assert(ds != null);
            Debug.Assert(constraintList != null);

            foreach (ArrayList list in constraintList)
            {
                Debug.Assert(list.Count == 5);
                var constraintName = (string)list[0];
                var parentInfo = (int[])list[1];
                var childInfo = (int[])list[2];
                var rules = (int[])list[3];
                var extendedProperties = (Hashtable)list[4];

                //ParentKey Columns.
                Debug.Assert(parentInfo.Length >= 1);
                var parentkeyColumns = new DataColumn[parentInfo.Length - 1];
                for (var i = 0; i < parentkeyColumns.Length; i++)
                {
                    Debug.Assert(ds.Tables.Count > parentInfo[0]);
                    Debug.Assert(ds.Tables[parentInfo[0]].Columns.Count > parentInfo[i + 1]);
                    parentkeyColumns[i] = ds.Tables[parentInfo[0]].Columns[parentInfo[i + 1]];
                }

                //ChildKey Columns.
                Debug.Assert(childInfo.Length >= 1);
                var childkeyColumns = new DataColumn[childInfo.Length - 1];
                for (var i = 0; i < childkeyColumns.Length; i++)
                {
                    Debug.Assert(ds.Tables.Count > childInfo[0]);
                    Debug.Assert(ds.Tables[childInfo[0]].Columns.Count > childInfo[i + 1]);
                    childkeyColumns[i] = ds.Tables[childInfo[0]].Columns[childInfo[i + 1]];
                }

                //Create the Constraint.
                var fk = new ForeignKeyConstraint(constraintName, parentkeyColumns, childkeyColumns);
                Debug.Assert(rules.Length == 3);
                fk.AcceptRejectRule = (AcceptRejectRule)rules[0];
                fk.UpdateRule = (Rule)rules[1];
                fk.DeleteRule = (Rule)rules[2];

                //Extended Properties.
                Debug.Assert(extendedProperties != null);
                if (extendedProperties.Keys.Count > 0)
                {
                    foreach (var propertyKey in extendedProperties.Keys)
                    {
                        fk.ExtendedProperties.Add(propertyKey, extendedProperties[propertyKey]);
                    }
                }

                //Add the constraint to the child datatable.
                Debug.Assert(ds.Tables.Count > childInfo[0]);
                ds.Tables[childInfo[0]].Constraints.Add(fk);
            }
        }

        /*
            Gets relations from the dataset.
            ***Serialized relations format : [relationName]->[parentTableIndex, parentcolumnIndexes]->[childTableIndex, childColumnIndexes]->[Nested]->[extendedProperties]***
        */

        private ArrayList GetRelations(DataSet ds)
        {
            Debug.Assert(ds != null);

            var relationList = new ArrayList();
            foreach (DataRelation rel in ds.Relations)
            {
                var relationName = rel.RelationName;
                var parentInfo = new int[rel.ParentColumns.Length + 1];
                parentInfo[0] = ds.Tables.IndexOf(rel.ParentTable);
                for (var j = 1; j < parentInfo.Length; j++)
                {
                    parentInfo[j] = rel.ParentColumns[j - 1].Ordinal;
                }

                var childInfo = new int[rel.ChildColumns.Length + 1];
                childInfo[0] = ds.Tables.IndexOf(rel.ChildTable);
                for (var j = 1; j < childInfo.Length; j++)
                {
                    childInfo[j] = rel.ChildColumns[j - 1].Ordinal;
                }

                var list = new ArrayList { relationName, parentInfo, childInfo, rel.Nested };
                var extendedProperties = new Hashtable();
                if (rel.ExtendedProperties.Keys.Count > 0)
                {
                    foreach (var propertyKey in rel.ExtendedProperties.Keys)
                    {
                        extendedProperties.Add(propertyKey, rel.ExtendedProperties[propertyKey]);
                    }
                }
                list.Add(extendedProperties);

                relationList.Add(list);
            }
            return relationList;
        }

        /*
            Adds relations to the dataset. The arraylist contains the serialized format of the relations.
            ***Deserialize the relations format : [relationName]->[parentTableIndex, parentcolumnIndexes]->[childTableIndex, childColumnIndexes]->[Nested]->[extendedProperties]***
        */

        private void SetRelations(DataSet ds, ArrayList relationList)
        {
            Debug.Assert(ds != null);
            Debug.Assert(relationList != null);

            foreach (ArrayList list in relationList)
            {
                Debug.Assert(list.Count == 5);
                var relationName = (string)list[0];
                var parentInfo = (int[])list[1];
                var childInfo = (int[])list[2];
                var isNested = (bool)list[3];
                var extendedProperties = (Hashtable)list[4];

                //ParentKey Columns.
                Debug.Assert(parentInfo.Length >= 1);
                var parentkeyColumns = new DataColumn[parentInfo.Length - 1];
                for (var i = 0; i < parentkeyColumns.Length; i++)
                {
                    Debug.Assert(ds.Tables.Count > parentInfo[0]);
                    Debug.Assert(ds.Tables[parentInfo[0]].Columns.Count > parentInfo[i + 1]);
                    parentkeyColumns[i] = ds.Tables[parentInfo[0]].Columns[parentInfo[i + 1]];
                }

                //ChildKey Columns.
                Debug.Assert(childInfo.Length >= 1);
                var childkeyColumns = new DataColumn[childInfo.Length - 1];
                for (var i = 0; i < childkeyColumns.Length; i++)
                {
                    Debug.Assert(ds.Tables.Count > childInfo[0]);
                    Debug.Assert(ds.Tables[childInfo[0]].Columns.Count > childInfo[i + 1]);
                    childkeyColumns[i] = ds.Tables[childInfo[0]].Columns[childInfo[i + 1]];
                }

                //Create the Relation, without any constraints[Assumption: The constraints are added earlier than the relations]
                var rel = new DataRelation(relationName, parentkeyColumns, childkeyColumns, false) { Nested = isNested };

                //Extended Properties.
                Debug.Assert(extendedProperties != null);
                if (extendedProperties.Keys.Count > 0)
                {
                    foreach (var propertyKey in extendedProperties.Keys)
                    {
                        rel.ExtendedProperties.Add(propertyKey, extendedProperties[propertyKey]);
                    }
                }

                //Add the relations to the dataset.
                ds.Relations.Add(rel);
            }
        }

        /*
            Suppress the read-only property and returns an arraylist of read-only columns.
        */

        private ArrayList SuppressReadOnly(DataSet ds)
        {
            Debug.Assert(ds != null);

            var readOnlyList = new ArrayList();
            for (var i = 0; i < ds.Tables.Count; i++)
            {
                var dt = ds.Tables[i];
                for (var j = 0; j < dt.Columns.Count; j++)
                {
                    if (dt.Columns[j].Expression == String.Empty && dt.Columns[j].ReadOnly)
                    {
                        dt.Columns[j].ReadOnly = false;
                        readOnlyList.Add(new[] { i, j });
                    }
                }
            }
            return readOnlyList;
        }

        /*
            Suppress the foreign key constraint rules and returns an arraylist of the existing foreignkey constraint rules.
        */

        private ArrayList SuppressConstraintRules(DataSet ds)
        {
            Debug.Assert(ds != null);

            var constraintRulesList = new ArrayList();
            for (var i = 0; i < ds.Tables.Count; i++)
            {
                var dtChild = ds.Tables[i];
                for (var j = 0; j < dtChild.Constraints.Count; j++)
                {
                    var c = dtChild.Constraints[j];
                    var fk = c as ForeignKeyConstraint;
                    if (fk == null) continue;
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
            return constraintRulesList;
        }

        /*
            Resets the read-only columns on the datatable based on the input readOnly list.
        */

        private void ResetReadOnly(DataSet ds, ArrayList readOnlyList)
        {
            Debug.Assert(ds != null);
            Debug.Assert(readOnlyList != null);

            foreach (var o in readOnlyList)
            {
                var indicesArr = (int[])o;

                Debug.Assert(indicesArr.Length == 2);
                var tableIndex = indicesArr[0];
                var columnIndex = indicesArr[1];

                Debug.Assert(ds.Tables.Count > tableIndex);
                Debug.Assert(ds.Tables[tableIndex].Columns.Count > columnIndex);

                var dc = ds.Tables[tableIndex].Columns[columnIndex];
                Debug.Assert(dc != null);

                dc.ReadOnly = true;
            }
        }

        /*
            Resets the foreignkey constraint rules on the dataset based on the input constraint rules list.
        */

        private void ResetConstraintRules(DataSet ds, ArrayList constraintRulesList)
        {
            Debug.Assert(ds != null);
            Debug.Assert(constraintRulesList != null);

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
            Checks whether the dataset name and namespaces are as expected and the tables count is right.
        */

        private bool IsSchemaIdentical(DataSet ds)
        {
            Debug.Assert(ds != null);
            if (ds.DataSetName != _datasetName || ds.Namespace != _namespace)
            {
                return false;
            }
            Debug.Assert(_dataTableSurrogates != null);
            
            return ds.Tables.Count == _dataTableSurrogates.Length;
        }
    }
}