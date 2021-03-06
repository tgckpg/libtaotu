﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

using Net.Astropenguin.Linq;
using Net.Astropenguin.Loaders;

namespace libtaotu.Dialogs
{
	using Models.Procedure;

	sealed partial class InputProcParam : ContentDialog
	{
		private ProcParameter Param;

		public bool Canceled { get; private set; }
		private SortedDictionary<int, TextBox> InputValues = new SortedDictionary<int, TextBox>();

		public InputProcParam( ProcParameter Param )
		{
			Canceled = true;

			this.InitializeComponent();
			this.Param = Param;

			SetTemplate();
		}

		private void SetTemplate()
		{
			StringResources stx = StringResources.Load( "/libtaotu/Message" );
			PrimaryButtonText = stx.Str( "OK" );
			SecondaryButtonText = stx.Str( "Cancel" );

			InputTitle.Text = Param.Caption;
			LayoutRoot.DataContext = Param;
		}

		private void ContentDialog_PrimaryButtonClick( ContentDialog sender, ContentDialogButtonClickEventArgs args )
		{
			Param.SetDefaults( InputValues.Remap( x => x.Value.Text ).ToArray() );
			Canceled = false;
		}

		private void RegisterInput( object sender, RoutedEventArgs e )
		{
			TextBox TextInput = ( TextBox ) sender;
			InputValues.Add( ( int ) TextInput.Tag, TextInput );
		}
	}
}