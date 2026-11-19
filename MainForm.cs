using Microsoft.Data.Sqlite;
using System.Data;
using System.Drawing;

namespace AcademicManager;

public class MainForm : Form
{
    readonly string db = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AcademicManager", "academic.db");
    readonly DataGridView grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.White };
    readonly TextBox search = new() { Width = 230, PlaceholderText = "Пошук за назвою або автором" };
    readonly Label stats = new() { AutoSize = true, Font = new Font("Segoe UI", 11, FontStyle.Bold) };
    readonly ComboBox filter = new() { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
    bool teachersMode;

    public MainForm()
    {
        Text = "Academic Manager — Облік наукових публікацій";
        Width = 1100; Height = 680; StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(245, 244, 250);
        InitDatabase();
        var header = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 66, Padding = new Padding(12), BackColor = Color.FromArgb(76, 51, 128), WrapContents = false };
        var title = new Label { Text = "ACADEMIC MANAGER", ForeColor = Color.White, Font = new Font("Segoe UI", 17, FontStyle.Bold), Width = 300, Height = 38 };
        header.Controls.Add(title);
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 95, Padding = new Padding(12), AutoScroll = true };
        Button AddButton(string name, Action action)
        {
            var b = new Button { Text = name, AutoSize = true, Height = 36, BackColor = Color.FromArgb(225, 215, 246), FlatStyle = FlatStyle.Flat, Margin = new Padding(3, 3, 7, 3) };
            b.Click += (_, _) => action(); toolbar.Controls.Add(b); return b;
        }
        AddButton("Публікації", () => { teachersMode = false; RefreshGrid(); });
        AddButton("Викладачі", () => { teachersMode = true; RefreshGrid(); });
        AddButton("Додати", () => EditItem(false));
        AddButton("Редагувати", () => EditItem(true));
        AddButton("Видалити", DeleteItem);
        AddButton("Оновити", RefreshGrid);
        toolbar.Controls.Add(search);
        filter.Items.AddRange(new object[] { "Усі типи", "Стаття", "Тези", "Посібник", "Монографія", "Інше" });
        filter.SelectedIndex = 0;
        toolbar.Controls.Add(filter);
        search.TextChanged += (_, _) => RefreshGrid();
        filter.SelectedIndexChanged += (_, _) =>
        {
            if (grid != null && search != null)
                RefreshGrid();
        };
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 45, Padding = new Padding(14) };
        bottom.Controls.Add(stats);
        Controls.Add(grid); Controls.Add(bottom); Controls.Add(toolbar); Controls.Add(header);
        Shown += (_, _) => RefreshGrid();
    }

    SqliteConnection Open()
    {
        var c = new SqliteConnection($"Data Source={db}"); c.Open();
        using var fk = c.CreateCommand(); fk.CommandText = "PRAGMA foreign_keys=ON"; fk.ExecuteNonQuery();
        return c;
    }
    void InitDatabase()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(db)!);
        using var c = Open(); using var cmd = c.CreateCommand();
        cmd.CommandText = "CREATE TABLE IF NOT EXISTS Teachers (Id INTEGER PRIMARY KEY AUTOINCREMENT, FullName TEXT NOT NULL, Department TEXT NOT NULL); CREATE TABLE IF NOT EXISTS Publications (Id INTEGER PRIMARY KEY AUTOINCREMENT, Title TEXT NOT NULL, Year INTEGER NOT NULL, Category TEXT NOT NULL, TeacherId INTEGER NOT NULL REFERENCES Teachers(Id) ON DELETE RESTRICT);";
        cmd.ExecuteNonQuery();
    }
    void RefreshGrid()
    {
        using var c = Open(); using var cmd = c.CreateCommand();
        if (teachersMode)
        {
            cmd.CommandText = "SELECT Id, FullName AS 'ПІБ', Department AS 'Відділення' FROM Teachers WHERE FullName LIKE $q OR Department LIKE $q ORDER BY FullName";
        }
        else
        {
            cmd.CommandText = "SELECT p.Id, p.Title AS 'Назва', p.Year AS 'Рік', p.Category AS 'Тип', t.FullName AS 'Автор' FROM Publications p JOIN Teachers t ON p.TeacherId=t.Id WHERE (p.Title LIKE $q OR t.FullName LIKE $q) AND ($category='Усі типи' OR p.Category=$category) ORDER BY p.Year DESC, p.Id DESC";
            cmd.Parameters.AddWithValue("$category", filter.SelectedItem?.ToString() ?? "Усі типи");
        }
        cmd.Parameters.AddWithValue("$q", "%" + search.Text + "%");
        using var reader = cmd.ExecuteReader();
        var table = new DataTable(); table.Load(reader);
        grid.DataSource = table;
        // DataGridView columns can be generated after binding; no early column access.
        using var count = c.CreateCommand();
        count.CommandText = "SELECT (SELECT COUNT(*) FROM Teachers), (SELECT COUNT(*) FROM Publications)";
        using var r = count.ExecuteReader(); r.Read();
        stats.Text = $"Викладачів: {r.GetInt64(0)}    |    Публікацій: {r.GetInt64(1)}    |    У таблиці: {table.Rows.Count}";
    }
    int SelectedId()
    {
        if (grid.CurrentRow?.DataBoundItem is DataRowView row &&
            row.Row.Table.Columns.Contains("Id") &&
            row["Id"] != DBNull.Value)
            return Convert.ToInt32(row["Id"]);
        return -1;
    }
    void DeleteItem()
    {
        int id = SelectedId(); if (id < 0) return;
        if (MessageBox.Show("Видалити вибраний запис?", "Підтвердження", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        try
        {
            using var c = Open(); using var cmd = c.CreateCommand();
            cmd.CommandText = teachersMode ? "DELETE FROM Teachers WHERE Id=$id" : "DELETE FROM Publications WHERE Id=$id";
            cmd.Parameters.AddWithValue("$id", id); cmd.ExecuteNonQuery(); RefreshGrid();
        }
        catch (SqliteException) { MessageBox.Show("Не можна видалити викладача, якщо у нього є публікації. Спочатку видаліть їх."); }
    }
    void EditItem(bool editing)
    {
        int id = editing ? SelectedId() : -1;
        if (editing && id < 0) { MessageBox.Show("Спочатку виберіть запис."); return; }
        using var c = Open();
        using var dlg = new Form { Text = editing ? "Редагування" : "Новий запис", Width = 480, Height = 340, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 2, RowCount = 5 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 4; i++) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        dlg.Controls.Add(layout);
        TextBox Field(string label, string value = "")
        {
            var box = new TextBox { Dock = DockStyle.Fill, Text = value };
            int row = layout.Controls.Count == 0 ? 0 : 1;
            layout.Controls.Add(new Label { Text = label, AutoSize = true }, 0, row);
            layout.Controls.Add(box, 1, row); return box;
        }
        string a = "", b = "", d = ""; int year = DateTime.Now.Year; int teacherId = -1;
        if (editing)
        {
            using var q = c.CreateCommand();
            q.CommandText = teachersMode ? "SELECT FullName, Department FROM Teachers WHERE Id=$id" : "SELECT Title, Year, Category, TeacherId FROM Publications WHERE Id=$id";
            q.Parameters.AddWithValue("$id", id);
            using var r = q.ExecuteReader(); if (!r.Read()) return;
            a = r.GetString(0);
            if (teachersMode) b = r.GetString(1);
            else { year = r.GetInt32(1); d = r.GetString(2); teacherId = r.GetInt32(3); }
        }
        TextBox first = Field(teachersMode ? "ПІБ" : "Назва", a);
        TextBox? second = null;
        NumericUpDown? years = null;
        ComboBox? categories = null, authors = null;
        if (teachersMode) second = Field("Відділення", b);
        else
        {
            layout.Controls.Add(new Label { Text = "Рік", AutoSize = true }, 0, 1);
            years = new NumericUpDown { Minimum = 1900, Maximum = 2100, Value = Math.Clamp(year, 1900, 2100), Dock = DockStyle.Fill };
            layout.Controls.Add(years, 1, 1);
            layout.Controls.Add(new Label { Text = "Тип", AutoSize = true }, 0, 2);
            categories = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            categories.Items.AddRange(new object[] { "Стаття", "Тези", "Посібник", "Монографія", "Інше" });
            categories.SelectedItem = editing ? d : "Стаття";
            if (categories.SelectedIndex < 0) categories.SelectedIndex = 0;
            layout.Controls.Add(categories, 1, 2);
            layout.Controls.Add(new Label { Text = "Автор", AutoSize = true }, 0, 3);
            authors = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Name", ValueMember = "Id" };
            using var q = c.CreateCommand(); q.CommandText = "SELECT Id, FullName FROM Teachers ORDER BY FullName";
            using var r = q.ExecuteReader();
            var authorTable = new DataTable(); authorTable.Columns.Add("Id", typeof(int)); authorTable.Columns.Add("Name", typeof(string));
            while (r.Read()) authorTable.Rows.Add(r.GetInt32(0), r.GetString(1));
            if (authorTable.Rows.Count == 0) { MessageBox.Show("Спочатку додайте хоча б одного викладача."); return; }
            authors.DataSource = authorTable;
            if (teacherId > 0) authors.SelectedValue = teacherId;
            layout.Controls.Add(authors, 1, 3);
        }
        var save = new Button { Text = "Зберегти", DialogResult = DialogResult.OK, Width = 130, Height = 36 };
        layout.Controls.Add(save, 1, 4); dlg.AcceptButton = save;
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        if (string.IsNullOrWhiteSpace(first.Text) || (teachersMode && string.IsNullOrWhiteSpace(second!.Text))) { MessageBox.Show("Заповніть усі обов'язкові поля."); return; }
        using var cmd = c.CreateCommand();
        if (teachersMode)
        {
            cmd.CommandText = editing ? "UPDATE Teachers SET FullName=$a, Department=$b WHERE Id=$id" : "INSERT INTO Teachers(FullName,Department) VALUES($a,$b)";
            cmd.Parameters.AddWithValue("$b", second!.Text.Trim());
        }
        else
        {
            cmd.CommandText = editing ? "UPDATE Publications SET Title=$a, Year=$year, Category=$category, TeacherId=$teacher WHERE Id=$id" : "INSERT INTO Publications(Title,Year,Category,TeacherId) VALUES($a,$year,$category,$teacher)";
            cmd.Parameters.AddWithValue("$year", (int)years!.Value);
            cmd.Parameters.AddWithValue("$category", categories!.SelectedItem!.ToString()!);
            cmd.Parameters.AddWithValue("$teacher", Convert.ToInt32(authors!.SelectedValue));
        }
        cmd.Parameters.AddWithValue("$a", first.Text.Trim()); cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery(); RefreshGrid();
    }
}
