namespace WPT.WinForms.Ui;

public static class UiStyler
{
    public static void ApplyForm(Form form)
    {
        form.BackColor = UiTheme.FormBackground;
        form.Font = UiTheme.UiFont;
        form.ForeColor = UiTheme.TextPrimary;
    }

    public static void StyleLabel(Label label, bool secondary = false)
    {
        label.ForeColor = secondary ? UiTheme.TextSecondary : UiTheme.TextPrimary;
        label.BackColor = Color.Transparent;
    }

    public static void StyleTextBox(TextBox textBox)
    {
        textBox.BorderStyle = BorderStyle.FixedSingle;
        textBox.BackColor = UiTheme.SurfaceElevated;
        textBox.ForeColor = UiTheme.TextPrimary;
    }

    public static void StyleComboBox(ComboBox comboBox)
    {
        comboBox.FlatStyle = FlatStyle.Flat;
        comboBox.BackColor = UiTheme.SurfaceElevated;
        comboBox.ForeColor = UiTheme.TextPrimary;
    }

    public static void StyleCheckBox(CheckBox checkBox)
    {
        checkBox.ForeColor = UiTheme.TextPrimary;
        checkBox.BackColor = UiTheme.TabActive;
        checkBox.UseVisualStyleBackColor = false;
        checkBox.FlatStyle = FlatStyle.Flat;
        checkBox.FlatAppearance.BorderColor = UiTheme.Border;
        checkBox.FlatAppearance.CheckedBackColor = UiTheme.Accent;
    }

    public static void StylePrimaryButton(Button button)
    {
        StyleButton(button, UiTheme.Accent, UiTheme.AccentHover, UiTheme.AccentPressed, UiTheme.TextPrimary);
    }

    public static void StyleSecondaryButton(Button button)
    {
        StyleButton(button, UiTheme.SurfaceElevated, Color.FromArgb(58, 62, 70), Color.FromArgb(42, 46, 52), UiTheme.TextPrimary);
    }

    public static void StyleDangerButton(Button button)
    {
        StyleButton(button, UiTheme.Danger, UiTheme.DangerHover, Color.FromArgb(140, 50, 50), UiTheme.TextPrimary);
    }

    public static void StyleAddButton(Button button)
    {
        StyleButton(button, UiTheme.Accent, UiTheme.AccentHover, UiTheme.AccentPressed, UiTheme.TextPrimary);
        button.Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold);
        button.Padding = new Padding(0);
    }

    public static void StyleAccentIconButton(Button button)
    {
        StyleAddButton(button);
    }

    public static void StyleTabPage(TabPage tabPage)
    {
        tabPage.BackColor = UiTheme.TabActive;
        tabPage.ForeColor = UiTheme.TextPrimary;
        tabPage.Padding = new Padding(10, 8, 10, 8);
    }

    public static void StyleStatusLabel(Label label)
    {
        label.ForeColor = UiTheme.TextSecondary;
        label.BackColor = Color.Transparent;
    }

    private static void StyleButton(Button button, Color normal, Color hover, Color pressed, Color text)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = normal;
        button.ForeColor = text;
        button.Font = UiTheme.ButtonFont;
        button.Cursor = Cursors.Hand;
        button.FlatAppearance.MouseOverBackColor = hover;
        button.FlatAppearance.MouseDownBackColor = pressed;
        button.Padding = new Padding(8, 4, 8, 4);
    }
}
