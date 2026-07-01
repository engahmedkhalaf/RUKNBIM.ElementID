[Setup]
AppName=RUKNBIM Smart Select
AppVersion=1.0.0
AppPublisher=RUKNBIM
DefaultDirName={userappdata}\Autodesk\ApplicationPlugins\RUKNBIM.SmartSelect.bundle
DefaultGroupName=RUKNBIM
Compression=lzma2
SolidCompression=yes
OutputDir=Output
OutputBaseFilename=RUKNBIM.SmartSelect.Setup
DisableDirPage=yes
DisableProgramGroupPage=yes
DirExistsWarning=no

[Files]
; Copy PackageContents.xml to the bundle root
Source: "C:\Users\sati7\AppData\Roaming\Autodesk\ApplicationPlugins\RUKNBIM.SmartSelect.bundle\PackageContents.xml"; DestDir: "{app}"; Flags: ignoreversion

; Copy all contents recursively (dlls, images, localizations)
Source: "C:\Users\sati7\AppData\Roaming\Autodesk\ApplicationPlugins\RUKNBIM.SmartSelect.bundle\Contents\*"; DestDir: "{app}\Contents"; Flags: ignoreversion recursesubdirs createallsubdirs

[Messages]
SetupAppTitle=Install RUKNBIM Smart Select
SetupWindowTitle=RUKNBIM Smart Select Installer

[Code]
var
  HeaderPanel: TPanel;
  HeaderTitleLabel: TLabel;
  HeaderSubtitleLabel: TLabel;
  BodyPanel: TPanel;
  TermsButton: TButton;
  FinishedLabel: TLabel;

procedure TermsButtonClick(Sender: TObject);
var
  ErrorCode: Integer;
begin
  ShellExec('open', 'https://github.com/engahmedkhalaf/RUKNBIM.SmartSelect', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
end;

procedure InitializeWizard();
begin
  // Set form size to match Autodesk installer
  WizardForm.ClientWidth := 500;
  WizardForm.ClientHeight := 360;
  WizardForm.Position := poScreenCenter;

  // Position OuterNotebook below the black header panel
  WizardForm.OuterNotebook.Width := WizardForm.ClientWidth;
  WizardForm.OuterNotebook.Height := WizardForm.ClientHeight - 90 - 60; // Leave 60px for the bottom buttons panel
  WizardForm.OuterNotebook.Left := 0;
  WizardForm.OuterNotebook.Top := 90;

  // 1. Create Black Header Panel
  HeaderPanel := TPanel.Create(WizardForm);
  HeaderPanel.Parent := WizardForm;
  HeaderPanel.Left := 0;
  HeaderPanel.Top := 0;
  HeaderPanel.Width := WizardForm.ClientWidth;
  HeaderPanel.Height := 90;
  HeaderPanel.BevelOuter := bvNone;
  HeaderPanel.Color := clBlack;
  HeaderPanel.ParentBackground := False;

  // Header Title
  HeaderTitleLabel := TLabel.Create(WizardForm);
  HeaderTitleLabel.Parent := HeaderPanel;
  HeaderTitleLabel.Left := 20;
  HeaderTitleLabel.Top := 15;
  HeaderTitleLabel.Caption := 'RUKNBIM';
  HeaderTitleLabel.Font.Name := 'Segoe UI';
  HeaderTitleLabel.Font.Size := 18;
  HeaderTitleLabel.Font.Style := [fsBold];
  HeaderTitleLabel.Font.Color := clWhite;

  // Header Subtitle
  HeaderSubtitleLabel := TLabel.Create(WizardForm);
  HeaderSubtitleLabel.Parent := HeaderPanel;
  HeaderSubtitleLabel.Left := 20;
  HeaderSubtitleLabel.Top := 50;
  HeaderSubtitleLabel.Caption := 'RUKNBIM Smart Select 1.0.0';
  HeaderSubtitleLabel.Font.Name := 'Segoe UI';
  HeaderSubtitleLabel.Font.Size := 12;
  HeaderSubtitleLabel.Font.Color := clWhite;

  // 2. Create Custom White Body Panel (used on Ready and Finished pages)
  BodyPanel := TPanel.Create(WizardForm);
  BodyPanel.Parent := WizardForm;
  BodyPanel.Left := 0;
  BodyPanel.Top := HeaderPanel.Height;
  BodyPanel.Width := WizardForm.ClientWidth;
  BodyPanel.Height := WizardForm.ClientHeight - HeaderPanel.Height - 60; // 60 for bottom buttons area
  BodyPanel.BevelOuter := bvNone;
  BodyPanel.Color := clWhite;
  BodyPanel.ParentBackground := False;

  // Hide default Ready to Install components from the standard WizardForm page so they don't leak
  WizardForm.ReadyLabel.Hide;
  WizardForm.ReadyMemo.Hide;

  // 3. Customize the Bottom Buttons Area
  WizardForm.Color := $F0F0F0;

  // Hide the back button completely
  WizardForm.BackButton.Hide;

  // Create "View Terms and Conditions" button on the bottom left
  TermsButton := TButton.Create(WizardForm);
  TermsButton.Parent := WizardForm;
  TermsButton.Caption := 'View Store Terms and Conditions';
  TermsButton.Width := 200;
  TermsButton.Height := WizardForm.CancelButton.Height;
  TermsButton.Left := 20;
  TermsButton.Top := WizardForm.CancelButton.Top;
  TermsButton.OnClick := @TermsButtonClick;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpReady then
  begin
    // Welcome / Ready screen: Show the custom BodyPanel with "Install Now"
    BodyPanel.Show;
    TermsButton.Show;
    WizardForm.OuterNotebook.Hide;

    // Reposition NextButton to act as "Install Now" button
    WizardForm.NextButton.Parent := BodyPanel;
    WizardForm.NextButton.Caption := '   Install Now';
    WizardForm.NextButton.Width := 160;
    WizardForm.NextButton.Height := 40;
    WizardForm.NextButton.Left := (BodyPanel.Width - WizardForm.NextButton.Width) div 2;
    WizardForm.NextButton.Top := (BodyPanel.Height - WizardForm.NextButton.Height) div 2;
    WizardForm.NextButton.ElevationRequired := True; // UAC Shield icon
    WizardForm.NextButton.Show;

    // Set Cancel button position on the bottom right
    WizardForm.CancelButton.Parent := WizardForm;
    WizardForm.CancelButton.Left := WizardForm.ClientWidth - WizardForm.CancelButton.Width - 20;
    WizardForm.CancelButton.Top := WizardForm.ClientHeight - WizardForm.CancelButton.Height - 15;
  end
  else if CurPageID = wpInstalling then
  begin
    // Installation screen: Hide custom BodyPanel, show progress page
    BodyPanel.Hide;
    TermsButton.Hide;
    WizardForm.OuterNotebook.Show;

    // Set page color to White
    WizardForm.InstallingPage.Color := clWhite;
    WizardForm.InnerPage.Color := clWhite;

    // Reposition progress bar and status text onto the white area
    WizardForm.StatusLabel.Parent := WizardForm.InstallingPage;
    WizardForm.StatusLabel.Left := 20;
    WizardForm.StatusLabel.Width := WizardForm.ClientWidth - 40;
    WizardForm.StatusLabel.Top := 30;

    WizardForm.ProgressGauge.Parent := WizardForm.InstallingPage;
    WizardForm.ProgressGauge.Left := 20;
    WizardForm.ProgressGauge.Width := WizardForm.ClientWidth - 40;
    WizardForm.ProgressGauge.Top := 60;
    WizardForm.ProgressGauge.Height := 24;

    // Hide filename label to keep it clean (like Autodesk installer)
    WizardForm.FileNameLabel.Hide;

    // Put NextButton back onto WizardForm and hide it (it is disabled during installation anyway)
    WizardForm.NextButton.Parent := WizardForm;
    WizardForm.NextButton.Hide;
  end
  else if CurPageID = wpFinished then
  begin
    // Finished screen: Custom styled finish page
    BodyPanel.Show;
    TermsButton.Hide;
    WizardForm.OuterNotebook.Hide;

    // Remove UAC Shield from NextButton (which is now "Finish")
    WizardForm.NextButton.Parent := WizardForm;
    WizardForm.NextButton.ElevationRequired := False;
    WizardForm.NextButton.Caption := 'Finish';
    WizardForm.NextButton.Width := 90;
    WizardForm.NextButton.Height := WizardForm.CancelButton.Height;
    WizardForm.NextButton.Left := WizardForm.CancelButton.Left;
    WizardForm.NextButton.Top := WizardForm.CancelButton.Top;
    WizardForm.NextButton.Show;

    // Hide Cancel button since setup is completed
    WizardForm.CancelButton.Hide;

    // Create success message label on the BodyPanel
    if FinishedLabel = nil then
    begin
      FinishedLabel := TLabel.Create(WizardForm);
      FinishedLabel.Parent := BodyPanel;
      FinishedLabel.Left := 20;
      FinishedLabel.Width := BodyPanel.Width - 40;
      FinishedLabel.Height := 80;
      FinishedLabel.AutoSize := False;
      FinishedLabel.WordWrap := True;
      FinishedLabel.Font.Name := 'Segoe UI';
      FinishedLabel.Font.Size := 12;
      FinishedLabel.Font.Color := clBlack;
    end;
    FinishedLabel.Caption := 'Installation completed successfully!' + #13#10#13#10 + 'RUKNBIM Smart Select is now installed and ready to use in Autodesk Navisworks.';
    FinishedLabel.Top := (BodyPanel.Height - FinishedLabel.Height) div 2;
  end;
end;
