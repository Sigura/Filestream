#region usings

using System;
using System.Configuration;
using System.Diagnostics;
using System.Linq.Expressions;
using Microsoft.Practices.EnterpriseLibrary.Common.Configuration;
using Microsoft.Practices.EnterpriseLibrary.Common.Configuration.ContainerModel;
using Microsoft.Practices.EnterpriseLibrary.Common.Configuration.Design;
using Microsoft.Practices.EnterpriseLibrary.Data.Configuration;
using Microsoft.Practices.EnterpriseLibrary.Data.Sql;
using Microsoft.Practices.EnterpriseLibrary.Logging.Configuration;
using Microsoft.Practices.EnterpriseLibrary.Logging.Formatters;

#endregion

namespace FileStream.Common.Logging
{
    /// <summary>
    ///     Configuration object for a <see cref="FormattedDatabaseTraceListener" />.
    /// </summary>
    //[ResourceDescription(typeof(DesignResources), "FormattedDatabaseTraceListenerDataDescription")]
    //[ResourceDisplayName(typeof(DesignResources), "FormattedDatabaseTraceListenerDataDisplayName")]
    [AddSateliteProviderCommand(@"connectionStrings", typeof (DatabaseSettings), @"DefaultDatabase",
        @"DatabaseInstanceName")]
    public class FormattedDatabaseTraceListenerData : TraceListenerData
    {
        private const string AddCategoryStoredProcNameProperty = @"addCategoryStoredProcName";
        private const string DatabaseInstanceNameProperty = @"databaseInstanceName";
        private const string FormatterNameProperty = @"formatter";
        private const string WriteLogStoredProcNameProperty = @"writeLogStoredProcName";

        /// <summary>
        ///     Initializes a <see cref="FormattedDatabaseTraceListenerData" />.
        /// </summary>
        public FormattedDatabaseTraceListenerData()
            : base(typeof (FormattedDatabaseTraceListener))
        {
            ListenerDataType = typeof (FormattedDatabaseTraceListenerData);
        }

        /// <summary>
        ///     Initializes a named instance of <see cref="FormattedDatabaseTraceListenerData" /> with
        ///     name, stored procedure name, databse instance name, and formatter name.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="writeLogStoredProcName">The stored procedure name for writing the log.</param>
        /// <param name="addCategoryStoredProcName">The stored procedure name for adding a category for this log.</param>
        /// <param name="databaseInstanceName">The database instance name.</param>
        /// <param name="formatterName">The formatter name.</param>
        public FormattedDatabaseTraceListenerData(string name,
            string writeLogStoredProcName,
            string addCategoryStoredProcName,
            string databaseInstanceName,
            string formatterName)
            : this(
                name,
                writeLogStoredProcName,
                addCategoryStoredProcName,
                databaseInstanceName,
                formatterName,
                TraceOptions.None,
                SourceLevels.All)
        {
        }

        /// <summary>
        ///     Initializes a named instance of <see cref="FormattedDatabaseTraceListenerData" /> with
        ///     name, stored procedure name, databse instance name, and formatter name.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="writeLogStoredProcName">The stored procedure name for writing the log.</param>
        /// <param name="addCategoryStoredProcName">The stored procedure name for adding a category for this log.</param>
        /// <param name="databaseInstanceName">The database instance name.</param>
        /// <param name="formatterName">The formatter name.</param>
        /// <param name="traceOutputOptions">The trace options.</param>
        /// <param name="filter">The filter to be applied</param>
        public FormattedDatabaseTraceListenerData(string name,
            string writeLogStoredProcName,
            string addCategoryStoredProcName,
            string databaseInstanceName,
            string formatterName,
            TraceOptions traceOutputOptions,
            SourceLevels filter)
            : base(name, typeof (FormattedDatabaseTraceListener), traceOutputOptions, filter)
        {
            DatabaseInstanceName = databaseInstanceName;
            WriteLogStoredProcName = writeLogStoredProcName;
            AddCategoryStoredProcName = addCategoryStoredProcName;
            Formatter = formatterName;
        }

        /// <summary>
        ///     Gets and sets the database instance name.
        /// </summary>
        [ConfigurationProperty(DatabaseInstanceNameProperty, IsRequired = true)]
        //[ResourceDescription(typeof(DesignResources), "FormattedDatabaseTraceListenerDataDatabaseInstanceNameDescription")]
        //[ResourceDisplayName(typeof(DesignResources), "FormattedDatabaseTraceListenerDataDatabaseInstanceNameDisplayName")]
        [Reference(typeof (ConnectionStringSettingsCollection), typeof (ConnectionStringSettings))]
        public string DatabaseInstanceName
        {
            get { return (string) base[DatabaseInstanceNameProperty]; }
            set { base[DatabaseInstanceNameProperty] = value; }
        }

        /// <summary>
        ///     Gets and sets the stored procedure name for writing the log.
        /// </summary>
        [ConfigurationProperty(WriteLogStoredProcNameProperty, IsRequired = true, DefaultValue = "WriteLog")]
        //[ResourceDescription(typeof(DesignResources), "FormattedDatabaseTraceListenerDataWriteLogStoredProcNameDescription")]
        //[ResourceDisplayName(typeof(DesignResources), "FormattedDatabaseTraceListenerDataWriteLogStoredProcNameDisplayName")]
        public string WriteLogStoredProcName
        {
            get { return (string) base[WriteLogStoredProcNameProperty]; }
            set { base[WriteLogStoredProcNameProperty] = value; }
        }

        /// <summary>
        ///     Gets and sets the stored procedure name for adding a category for this log.
        /// </summary>
        [ConfigurationProperty(AddCategoryStoredProcNameProperty, IsRequired = true, DefaultValue = "AddCategory")]
        //[ResourceDescription(typeof(DesignResources), "FormattedDatabaseTraceListenerDataAddCategoryStoredProcNameDescription")]
        //[ResourceDisplayName(typeof(DesignResources), "FormattedDatabaseTraceListenerDataAddCategoryStoredProcNameDisplayName")]
        public string AddCategoryStoredProcName
        {
            get { return (string) base[AddCategoryStoredProcNameProperty]; }
            set { base[AddCategoryStoredProcNameProperty] = value; }
        }

        /// <summary>
        ///     Gets and sets the formatter name.
        /// </summary>
        [ConfigurationProperty(FormatterNameProperty, IsRequired = false)]
        //[ResourceDescription(typeof(DesignResources), "FormattedDatabaseTraceListenerDataFormatterDescription")]
        //[ResourceDisplayName(typeof(DesignResources), "FormattedDatabaseTraceListenerDataFormatterDisplayName")]
        [Reference(typeof (NameTypeConfigurationElementCollection<FormatterData, CustomFormatterData>),
            typeof (FormatterData))]
        public string Formatter
        {
            get { return (string) base[FormatterNameProperty]; }
            set { base[FormatterNameProperty] = value; }
        }

        /// <summary>
        ///     Returns a lambda expression that represents the creation of the trace listener described by this
        ///     configuration object.
        /// </summary>
        /// <returns>A lambda expression to create a trace listener.</returns>
        protected override Expression<Func<TraceListener>> GetCreationExpression()
        {
            var connectionString = ConfigurationManager.ConnectionStrings[DatabaseInstanceName].ConnectionString;

            return () => new FormattedDatabaseTraceListener(
                                 new SqlDatabase(connectionString),
                                 //Container.ResolvedIfNotNull<Database>(DatabaseInstanceName),
                                 WriteLogStoredProcName,
                                 AddCategoryStoredProcName,
                                 Container.ResolvedIfNotNull<ILogFormatter>(Formatter));
        }
    }
}