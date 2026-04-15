using System;
using System.Drawing;
using System.Windows.Forms;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Document = Autodesk.AutoCAD.ApplicationServices.Document;
using Exception = System.Exception;
using Font = System.Drawing.Font;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;
using CoreSectionSettings = SectionGenerator.Core.Sections.SectionSettings;
using SectionGenerator.Infrastructure.Config;

[assembly: CommandClass(typeof(SectionGenerator.ToolboxCommands))]

namespace SectionGenerator
{
    public class ToolboxCommands
    {
        private static PaletteSet? _toolboxPalette = null;
        private const string PaletteName = "CAD工具箱";

        [CommandMethod("ShowToolbox")]
        public void ShowToolbox()
        {
            if (_toolboxPalette == null)
            {
                CreateToolboxPanel();
            }
            _toolboxPalette.Visible = true;
        }

        [CommandMethod("HideToolbox")]
        public void HideToolbox()
        {
            if (_toolboxPalette != null)
            {
                _toolboxPalette.Visible = false;
            }
        }

        private void CreateToolboxPanel()
        {
            _toolboxPalette = new PaletteSet(PaletteName)
            {
                Size = new Size(280, 500),
                DockEnabled = (DockSides)((int)DockSides.Left + (int)DockSides.Right)
            };

            ToolboxControl control = new ToolboxControl();
            _toolboxPalette.Add(PaletteName, control);
        }
    }

    public class ToolboxControl : UserControl
    {
        private Button btnGenSection;
        private Button btnViewLine;
        private Button btnSettings;
        private Label lblTitle;
        private GroupBox grpSection;
        private GroupBox grpTools;
        private Panel pnlHeader;

        public ToolboxControl()
        {
            InitializeComponent();
            this.BackColor = Color.FromArgb(240, 240, 240);
        }

        private void InitializeComponent()
        {
            this.Size = new Size(280, 500);

            // 标题面板
            pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = Color.FromArgb(0, 122, 204)
            };

            lblTitle = new Label
            {
                Text = "CAD 工具箱",
                Font = new Font("微软雅黑", 14, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(10, 12)
            };
            pnlHeader.Controls.Add(lblTitle);

            // 剖面生成组
            grpSection = new GroupBox
            {
                Text = "剖面生成",
                Font = new Font("微软雅黑", 10, FontStyle.Bold),
                Location = new Point(10, 60),
                Size = new Size(250, 120),
                BackColor = Color.FromArgb(240, 240, 240)
            };

            btnGenSection = new Button
            {
                Text = "生成剖面图",
                Font = new Font("微软雅黑", 10),
                Location = new Point(20, 30),
                Size = new Size(210, 35),
                BackColor = Color.FromArgb(0, 150, 136),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnGenSection.FlatAppearance.BorderSize = 0;
            btnGenSection.Click += BtnGenSection_Click;

            btnSettings = new Button
            {
                Text = "剖面设置",
                Font = new Font("微软雅黑", 9),
                Location = new Point(20, 75),
                Size = new Size(100, 30),
                BackColor = Color.FromArgb(96, 125, 139),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnSettings.FlatAppearance.BorderSize = 0;
            btnSettings.Click += BtnSettings_Click;

            grpSection.Controls.Add(btnGenSection);
            grpSection.Controls.Add(btnSettings);

            // 工具组
            grpTools = new GroupBox
            {
                Text = "辅助工具",
                Font = new Font("微软雅黑", 10, FontStyle.Bold),
                Location = new Point(10, 190),
                Size = new Size(250, 100),
                BackColor = Color.FromArgb(240, 240, 240)
            };

            btnViewLine = new Button
            {
                Text = "查看线信息",
                Font = new Font("微软雅黑", 10),
                Location = new Point(20, 30),
                Size = new Size(210, 35),
                BackColor = Color.FromArgb(63, 81, 181),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnViewLine.FlatAppearance.BorderSize = 0;
            btnViewLine.Click += BtnViewLine_Click;

            grpTools.Controls.Add(btnViewLine);

            // 添加控件
            this.Controls.Add(pnlHeader);
            this.Controls.Add(grpSection);
            this.Controls.Add(grpTools);
        }

        private void BtnGenSection_Click(object? sender, EventArgs e)
        {
            ExecuteCommand("GenSection");
        }

        private void BtnViewLine_Click(object? sender, EventArgs e)
        {
            ExecuteCommand("ViewLine");
        }

        private void BtnSettings_Click(object? sender, EventArgs e)
        {
            ShowSettingsDialog();
        }

        private void ExecuteCommand(string commandName)
        {
            try
            {
                Document doc = Application.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    doc.SendStringToExecute(commandName + " ", true, false, true);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"执行命令失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowSettingsDialog()
        {
            using (SettingsForm settingsForm = new SettingsForm())
            {
                settingsForm.ShowDialog();
            }
        }
    }

    public class SettingsForm : Form
    {
        private TextBox txtBottomSlab;
        private TextBox txtTopSlab;
        private TextBox txtFinish;
        private TextBox txtFloorHeight;
        private CheckBox chkSlope;
        private TextBox txtSlope;
        private Button btnSave;
        private Button btnCancel;
        private CoreSectionSettings _settings;
        private readonly JsonConfigManager _config = new JsonConfigManager();

        public SettingsForm()
        {
            _settings = LoadSettings();
            InitializeComponent();
            LoadSettingsToForm();
        }

        private void InitializeComponent()
        {
            this.Text = "剖面设置";
            this.Size = new Size(350, 350);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.White;

            int y = 20;
            int labelWidth = 120;
            int inputWidth = 150;

            // 底板厚度
            Label lblBottom = new Label { Text = "底板厚度 (mm):", Location = new Point(20, y), Width = labelWidth };
            txtBottomSlab = new TextBox { Location = new Point(150, y), Width = inputWidth };
            this.Controls.Add(lblBottom);
            this.Controls.Add(txtBottomSlab);
            y += 35;

            // 顶板厚度
            Label lblTop = new Label { Text = "顶板厚度 (mm):", Location = new Point(20, y), Width = labelWidth };
            txtTopSlab = new TextBox { Location = new Point(150, y), Width = inputWidth };
            this.Controls.Add(lblTop);
            this.Controls.Add(txtTopSlab);
            y += 35;

            // 装修面层厚度
            Label lblFinish = new Label { Text = "装修厚度 (mm):", Location = new Point(20, y), Width = labelWidth };
            txtFinish = new TextBox { Location = new Point(150, y), Width = inputWidth };
            this.Controls.Add(lblFinish);
            this.Controls.Add(txtFinish);
            y += 35;

            // 层高
            Label lblHeight = new Label { Text = "层高 (mm):", Location = new Point(20, y), Width = labelWidth };
            txtFloorHeight = new TextBox { Location = new Point(150, y), Width = inputWidth };
            this.Controls.Add(lblHeight);
            this.Controls.Add(txtFloorHeight);
            y += 35;

            // 坡度复选框
            chkSlope = new CheckBox { Text = "楼板有坡度", Location = new Point(20, y), Width = labelWidth };
            chkSlope.CheckedChanged += ChkSlope_CheckedChanged;
            this.Controls.Add(chkSlope);
            y += 35;

            // 坡度值
            Label lblSlope = new Label { Text = "坡度值 (‰):", Location = new Point(20, y), Width = labelWidth };
            txtSlope = new TextBox { Location = new Point(150, y), Width = inputWidth, Enabled = false };
            this.Controls.Add(lblSlope);
            this.Controls.Add(txtSlope);
            y += 50;

            // 按钮
            btnSave = new Button
            {
                Text = "保存",
                Location = new Point(80, y),
                Size = new Size(80, 30),
                BackColor = Color.FromArgb(0, 150, 136),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += BtnSave_Click;

            btnCancel = new Button
            {
                Text = "取消",
                Location = new Point(180, y),
                Size = new Size(80, 30),
                BackColor = Color.FromArgb(158, 158, 158),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) => this.DialogResult = DialogResult.Cancel;

            this.Controls.Add(btnSave);
            this.Controls.Add(btnCancel);
        }

        private void ChkSlope_CheckedChanged(object? sender, EventArgs e)
        {
            txtSlope.Enabled = chkSlope.Checked;
        }

        private void LoadSettingsToForm()
        {
            txtBottomSlab.Text = _settings.BottomSlabThickness.ToString();
            txtTopSlab.Text = _settings.TopSlabThickness.ToString();
            txtFinish.Text = _settings.FinishThickness.ToString();
            txtFloorHeight.Text = _settings.FloorHeight.ToString();
            chkSlope.Checked = _settings.HasSlope;
            txtSlope.Text = (_settings.SlopeValue * 1000).ToString();
            txtSlope.Enabled = _settings.HasSlope;
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            try
            {
                _settings.BottomSlabThickness = double.Parse(txtBottomSlab.Text);
                _settings.TopSlabThickness = double.Parse(txtTopSlab.Text);
                _settings.FinishThickness = double.Parse(txtFinish.Text);
                _settings.FloorHeight = double.Parse(txtFloorHeight.Text);
                _settings.HasSlope = chkSlope.Checked;
                if (_settings.HasSlope)
                {
                    _settings.SlopeValue = double.Parse(txtSlope.Text) / 1000.0;
                }
                SaveSettings(_settings);
                this.DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存设置失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private CoreSectionSettings LoadSettings()
        {
            string configPath = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "",
                "SectionGeneratorConfig.json");

            return _config.LoadOrCreate(configPath, new CoreSectionSettings());
        }

        private void SaveSettings(CoreSectionSettings settings)
        {
            string configPath = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "",
                "SectionGeneratorConfig.json");
            _config.Save(configPath, settings);
        }
    }
}

