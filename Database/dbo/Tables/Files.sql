CREATE TABLE [dbo].[Files] (
    [ID]        UNIQUEIDENTIFIER           CONSTRAINT [DF_Files_ID] DEFAULT (newid()) ROWGUIDCOL NOT NULL,
    [Name]      NVARCHAR (255)             NOT NULL,
    [Body]      VARBINARY (MAX) FILESTREAM CONSTRAINT [DF__Files__Body__1920BF5C] DEFAULT (CONVERT([varbinary](max),'',(0))) NOT NULL,
    [Size]      AS                         (datalength([Body])) PERSISTED,
    [Extension] AS                         (reverse(substring(reverse([Name]),(0),charindex('.',reverse([Name]))))) PERSISTED,
    [Length]    BIGINT                     CONSTRAINT [DF_Files_Length] DEFAULT ((0)) NOT NULL,
    CONSTRAINT [PK_Files] PRIMARY KEY CLUSTERED ([ID] ASC)
) FILESTREAM_ON [fsGroup];


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'(reverse(substring(reverse([Name]),(0),charindex(''.'',reverse([Name])))))', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'Files', @level2type = N'COLUMN', @level2name = N'Extension';

