﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

using Net.Astropenguin.IO;
using Net.Astropenguin.Logging;

namespace GFlow.Models.Procedure
{
	using Controls;
	using Interfaces;

	class ProcResult : Procedure, IProcessList
	{
		public static readonly string ID = typeof( ProcResult ).Name;

		public IList<IProcessNode> ProcessNodes { get; set; }

		public RunMode Mode { get; set; }
		public bool IsOutput { get { return Mode == RunMode.OUTPUT; } }

		public string RawModeName { get; private set; }
		public string ModeName { get { return ProcStrRes.Str( RawModeName ); } }

		public string Key { get; set; }
		public Dictionary<string, string> PrefixMap { get; set; }

		public override Type PropertyPage => typeof( Dialogs.EditProcResult );

		public ProcResult()
			: base( ProcType.RESULT )
		{
			Key = "Key-1";
			ProcessNodes = new ObservableCollection<IProcessNode>();
			SetMode( RunMode.DEFINE );
		}

		public void ToggleMode()
		{
			switch ( Mode )
			{
				case RunMode.OUTPUT:
					SetMode( RunMode.DEFINE );
					break;
				case RunMode.DEFINE:
				default:
					SetMode( RunMode.OUTPUT );
					break;
			}
		}

		public override async Task<ProcConvoy> Run( ICrawler Crawler, ProcConvoy Convoy )
		{
			await base.Run( Crawler, Convoy );

			ProcConvoy ThisUsableConvoy;

			bool HasUsableConvoy = TryGetConvoy( out ThisUsableConvoy, ( P, C ) =>
				C.Payload is IEnumerable<IStorageFile>
				|| C.Payload is IEnumerable<string>
				|| C.Payload is IStorageFile
				|| C.Payload is string
			);

			IStorageFile OutputTmp = await AppStorage.MkTemp();

			if ( Mode == RunMode.OUTPUT )
			{
				ProcConvoy UsableConvoy;
				foreach ( OutputDef Def in ProcessNodes )
				{
					if ( Def.Key == Key && HasUsableConvoy )
					{
						object Payload = await SubprocRun( Crawler, Def, ThisUsableConvoy.Payload );
						await AppendResult( OutputTmp, Payload );
					}
					else if ( TryGetConvoy( out UsableConvoy, ( P, C ) =>
						P is ProcResult
						&& ( ( ProcResult ) P ).Key == Def.Key
						// Because ProcResult only returns IEnumerable<IStorageFile>
						&& C.Payload is IEnumerable<IStorageFile>
					) )
					{
						object Payload = await SubprocRun( Crawler, Def, UsableConvoy.Payload );
						await AppendResult( OutputTmp, Payload );
					}
					else
					{
						Crawler.PLog( this, Res.RSTR( "ResultKeyNotFound", Def.Key ), LogType.WARNING );
					}
				}
			}
			else
			{
				if ( !HasUsableConvoy ) return Convoy;
				await AppendResult( OutputTmp, ThisUsableConvoy.Payload );
			}

			return new ProcConvoy( this, new IStorageFile[] { OutputTmp } );
		}

		private async Task AppendResult( IStorageFile File, object Result )
		{
			if ( Result is string )
			{
				await File.WriteString( ( ( string ) Result ) + '\n', true );
			}
			else if( Result is IEnumerable<string> )
			{
				await File.WriteString( string.Join( "\n", ( IEnumerable<string> ) Result ) + "\n", true );
			}
			else if( Result is IStorageFile )
			{
				await File.WriteFile( ( IStorageFile ) Result, true, new byte[] { ( byte ) '\n' } );
			}
			else if( Result is IEnumerable<IStorageFile> )
			{
				foreach ( IStorageFile ISF in ( ( IEnumerable<IStorageFile> ) Result ) )
					await File.WriteFile( ISF, true, new byte[] { ( byte ) '\n' } );
			}
		}

		private async Task<object> SubprocRun( ICrawler Crawler, OutputDef Def, object Input )
		{
			if ( Def != null && Def.SubProcedures.HasProcedures )
			{
				Crawler.PLog( this, Res.RSTR( "SubProcRun" ), LogType.INFO );
				ProcConvoy SubConvoy = await Def.SubProcedures.CreateSpider( Crawler ).Crawl( new ProcConvoy( this, Input ) );

				// Process ReceivedConvoy
				if ( SubConvoy.Payload is string
					|| SubConvoy.Payload is IEnumerable<string>
					|| SubConvoy.Payload is IStorageFile
					|| SubConvoy.Payload is IEnumerable<IStorageFile> )
					return SubConvoy.Payload;
			}

			return Input;
		}

		public override void ReadParam( XParameter Param )
		{
			base.ReadParam( Param );

			Key = Param.GetValue( "Key" ) ?? "Key-1";

			XParameter[] ExtParams = Param.Parameters( "i" );
			foreach ( XParameter ExtParam in ExtParams )
			{
				ProcessNodes.Add( new OutputDef( ExtParam ) );
			}

			SetMode( Param.GetValue( "Mode" ) );
		}

		public override XParameter ToXParam()
		{
			XParameter Param = base.ToXParam();

			Param.SetValue( new XKey[] {
				new XKey( "Key", Key )
				, new XKey( "Mode", RawModeName )
			} );

			int i = 0;
			foreach( OutputDef Extr in ProcessNodes )
			{
				XParameter ExtParam = Extr.ToXParam();
				ExtParam.Id += i;
				ExtParam.SetValue( new XKey( "i", i++ ) );

				Param.SetParameter( ExtParam );
			}

			return Param;
		}

		private void SetMode( string name )
		{
			switch ( name )
			{
				case "OUTPUT":
					SetMode( RunMode.OUTPUT );
					break;
				case "DEFINE":
				default:
					SetMode( RunMode.DEFINE );
					break;
			}
		}

		private void SetMode( RunMode Mode )
		{
			this.Mode = Mode;
			RawModeName = Enum.GetName( typeof( RunMode ), Mode );
			NotifyChanged( "RawModeName", "ModeName", "IsOutput" );
		}

		public class OutputDef : IProcessNode, IGFLabelOwner
		{
			public ProcManager SubProcedures { get; set; }

			public string Label => Key;

			public string Key { get; set; }
			public bool HasSubProcs => SubProcedures.HasProcedures;

			public OutputDef()
			{
				SubProcedures = new ProcManager();
				Key = "Key-1";
			}

			public OutputDef( XParameter Param )
				: this()
			{
				Key = Param.GetValue( "Key" ) ?? "Key-1";

				XParameter Sub = Param.Parameter( "SubProc" );
				if ( Sub != null ) SubProcedures.ReadParam( Sub );
			}

			public XParameter ToXParam()
			{
				XParameter Param = new XParameter( "OutputItem" );

				Param.SetValue( new XKey( "Key", Key ) );

				XParameter SubParam = SubProcedures.ToXParam();
				SubParam.Id = "SubProc";
				Param.SetParameter( SubParam );

				return Param;
			}
		}

	}
}