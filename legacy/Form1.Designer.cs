namespace AudioControlApp
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            this.button1 = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.button3 = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.notifyIcon1 = new System.Windows.Forms.NotifyIcon(this.components);
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.menuCurrent = new System.Windows.Forms.ToolStripMenuItem();
            this.menuItem51 = new System.Windows.Forms.ToolStripMenuItem();
            this.menuItem20 = new System.Windows.Forms.ToolStripMenuItem();
            this.menuShow = new System.Windows.Forms.ToolStripMenuItem();
            this.autoStartupItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showInterface = new System.Windows.Forms.ToolStripMenuItem();
            this.contextMenuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(489, 137);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(261, 106);
            this.button1.TabIndex = 0;
            this.button1.Text = "5.1";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.menuItem51_Click);
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(489, 291);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(261, 98);
            this.button2.TabIndex = 1;
            this.button2.Text = "2.0";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.menuItem20_Click);
            // 
            // button3
            // 
            this.button3.Location = new System.Drawing.Point(80, 291);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(261, 98);
            this.button3.TabIndex = 2;
            this.button3.Text = "Open Setting";
            this.button3.UseVisualStyleBackColor = true;
            this.button3.Click += new System.EventHandler(this.button3_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(80, 68);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(82, 31);
            this.label1.TabIndex = 3;
            this.label1.Text = "label1";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(80, 137);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(82, 31);
            this.label2.TabIndex = 4;
            this.label2.Text = "label2";
            // 
            // notifyIcon1
            // 
            this.notifyIcon1.ContextMenuStrip = this.contextMenuStrip1;
            this.notifyIcon1.Icon = ((System.Drawing.Icon)(resources.GetObject("notifyIcon1.Icon")));
            this.notifyIcon1.Text = "notifyIcon1";
            this.notifyIcon1.Visible = true;
            this.notifyIcon1.MouseClick += new System.Windows.Forms.MouseEventHandler(this.notifyIcon1_MouseClick);
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.ImageScalingSize = new System.Drawing.Size(32, 32);
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuCurrent,
            this.menuItem51,
            this.menuItem20,
            this.menuShow,
            this.autoStartupItem,
            this.showInterface});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(185, 232);
            // 
            // menuCurrent
            // 
            this.menuCurrent.Name = "menuCurrent";
            this.menuCurrent.Size = new System.Drawing.Size(184, 38);
            this.menuCurrent.Text = "当前";
            // 
            // menuItem51
            // 
            this.menuItem51.Name = "menuItem51";
            this.menuItem51.Size = new System.Drawing.Size(184, 38);
            this.menuItem51.Text = "5.1";
            this.menuItem51.Click += new System.EventHandler(this.menuItem51_Click);
            // 
            // menuItem20
            // 
            this.menuItem20.Name = "menuItem20";
            this.menuItem20.Size = new System.Drawing.Size(184, 38);
            this.menuItem20.Text = "2.0";
            this.menuItem20.Click += new System.EventHandler(this.menuItem20_Click);
            // 
            // menuShow
            // 
            this.menuShow.Name = "menuShow";
            this.menuShow.Size = new System.Drawing.Size(184, 38);
            this.menuShow.Text = "显示设置";
            this.menuShow.Click += new System.EventHandler(this.menuShow_Click);
            // 
            // autoStartupItem
            // 
            this.autoStartupItem.Name = "autoStartupItem";
            this.autoStartupItem.Size = new System.Drawing.Size(184, 38);
            this.autoStartupItem.Text = "开机启动";
            this.autoStartupItem.Click += new System.EventHandler(this.AutoStartup_Click);
            // 
            // showInterface
            // 
            this.showInterface.Name = "showInterface";
            this.showInterface.Size = new System.Drawing.Size(184, 38);
            this.showInterface.Text = "显示界面";
            this.showInterface.Click += new System.EventHandler(this.showInterface_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(14F, 31F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.button3);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.button1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "Form1";
            this.Text = "Form1";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
            this.Load += new System.EventHandler(this.Form1_Load);
            this.SizeChanged += new System.EventHandler(this.Form1_SizeChanged);
            this.contextMenuStrip1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private Button button1;
        private Button button2;
        private Button button3;
        private Label label1;
        private Label label2;
        private NotifyIcon notifyIcon1;
        private ContextMenuStrip contextMenuStrip1;
        private ToolStripMenuItem toolStripMenuItem1;
        private ToolStripMenuItem menuCurrent;
        private ToolStripMenuItem menuItem51;
        private ToolStripMenuItem menuItem20;
        private ToolStripMenuItem menuShow;
        private ToolStripMenuItem autoStartupItem;
        private ToolStripMenuItem showInterface;
    }
}