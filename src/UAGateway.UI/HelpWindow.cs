using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Globalization;
using Windows.Graphics;
using Windows.System;
using WinRT.Interop;

namespace UAGateway.UI;

public sealed class HelpWindow : Window
{
    private readonly List<HelpTopic> _topics;
    private readonly TextBox _searchBox;
    private readonly TreeView _topicTree;
    private readonly TextBlock _contentTitle;
    private readonly TextBlock _contentPath;
    private readonly MarkdownTextBlock _markdownBlock;

    public HelpWindow()
    {
        Title = "UA Gateway Help Center";

        _topics = LoadHelpTopics();

        _searchBox = new TextBox { PlaceholderText = "Search help topics" };

        _topicTree = new TreeView
        {
            MinWidth = 420,
            SelectionMode = TreeViewSelectionMode.Single,
        };

        _contentTitle = new TextBlock
        {
            FontSize = 20,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Text = "Help",
        };

        _contentPath = new TextBlock
        {
            Opacity = 0.7,
            Text = "Select a topic from the list.",
        };

        _markdownBlock = new MarkdownTextBlock
        {
            IsTextSelectionEnabled = true,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        Content = BuildLayout();

        _searchBox.TextChanged += (_, _) => ApplyFilter();
        _topicTree.SelectionChanged += TopicTree_SelectionChanged;
        _markdownBlock.LinkClicked += MarkdownBlock_LinkClicked;

        ApplyFilter();
        ConfigureWindowBounds();
    }

    private UIElement BuildLayout()
    {
        var root = new Grid
        {
            Padding = new Thickness(16),
            ColumnSpacing = 12,
            RowSpacing = 8,
        };

        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(420) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var leftPanel = new Grid { RowSpacing = 8 };
        leftPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        leftPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        leftPanel.Children.Add(_searchBox);
        Grid.SetRow(_topicTree, 1);
        leftPanel.Children.Add(_topicTree);

        var markdownScroller = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _markdownBlock,
        };

        var rightPanel = new Grid { RowSpacing = 8 };
        rightPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rightPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rightPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        rightPanel.Children.Add(_contentTitle);
        Grid.SetRow(_contentPath, 1);
        rightPanel.Children.Add(_contentPath);
        Grid.SetRow(markdownScroller, 2);
        rightPanel.Children.Add(markdownScroller);

        root.Children.Add(leftPanel);
        Grid.SetColumn(rightPanel, 1);
        root.Children.Add(rightPanel);

        return root;
    }

    private void ConfigureWindowBounds()
    {
        try
        {
            var hWnd = WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            appWindow.Resize(new SizeInt32(1480, 960));
            appWindow.Move(new PointInt32(120, 90));
        }
        catch
        {
            // Best-effort only.
        }
    }

    private void ApplyFilter()
    {
        var query = (_searchBox.Text ?? string.Empty).Trim();

        var filtered = _topics
            .Where(topic =>
                query.Length == 0 ||
                topic.Category.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                topic.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                topic.Content.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(topic => topic.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(topic => topic.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _topicTree.RootNodes.Clear();

        foreach (var categoryGroup in filtered.GroupBy(topic => topic.Category))
        {
            var categoryNode = new TreeViewNode
            {
                Content = categoryGroup.Key,
                IsExpanded = true,
            };

            foreach (var topic in categoryGroup)
            {
                categoryNode.Children.Add(new TreeViewNode { Content = topic });
            }

            _topicTree.RootNodes.Add(categoryNode);
        }

        if (filtered.Count == 0)
        {
            _contentTitle.Text = "No results";
            _contentPath.Text = "Try a different search query.";
            _markdownBlock.Text = "No help topics matched your search.";
            return;
        }

        ShowTopic(filtered[0]);
        if (_topicTree.RootNodes.Count > 0 && _topicTree.RootNodes[0].Children.Count > 0)
        {
            _topicTree.SelectedNode = _topicTree.RootNodes[0].Children[0];
        }
    }

    private void TopicTree_SelectionChanged(TreeView sender, TreeViewSelectionChangedEventArgs args)
    {
        if (sender.SelectedNode?.Content is not HelpTopic topic)
        {
            return;
        }

        ShowTopic(topic);
    }

    private void ShowTopic(HelpTopic topic)
    {
        _contentTitle.Text = $"{topic.Category} / {topic.Title}";
        _contentPath.Text = topic.RelativePath;
        _markdownBlock.Text = topic.Content;
    }

    private async void MarkdownBlock_LinkClicked(object? sender, LinkClickedEventArgs args)
    {
        var link = args.Link;

        // Absolute URL — open in default browser.
        if (Uri.TryCreate(link, UriKind.Absolute, out var absoluteUri) &&
            (absoluteUri.Scheme == Uri.UriSchemeHttp || absoluteUri.Scheme == Uri.UriSchemeHttps))
        {
            await Launcher.LaunchUriAsync(absoluteUri);
            return;
        }

        // Relative doc link — find matching topic by filename and navigate.
        var fileName = Path.GetFileName(link);
        var match = _topics.FirstOrDefault(t =>
            Path.GetFileName(t.RelativePath).Equals(fileName, StringComparison.OrdinalIgnoreCase));

        if (match is not null)
        {
            ShowTopic(match);
            SelectTopicInTree(match);
        }
    }

    private void SelectTopicInTree(HelpTopic topic)
    {
        foreach (var categoryNode in _topicTree.RootNodes)
        {
            foreach (var leafNode in categoryNode.Children)
            {
                if (leafNode.Content is HelpTopic t && t == topic)
                {
                    _topicTree.SelectedNode = leafNode;
                    return;
                }
            }
        }
    }

    private static List<HelpTopic> LoadHelpTopics()
    {
        var docsDirectory = ResolveDocsDirectoryPath();
        if (docsDirectory is null)
        {
            return [];
        }

        var requestedTopics = new (string Category, string Title, string RelativePath)[]
        {
            ("Get Started", "Help Home", "HELP.md"),
            ("Get Started", "Project Context", "PROJECT_CONTEXT.md"),
            ("Operations", "Operations and Developer Guide", "OPERATIONS_AND_DEV_GUIDE.md"),
            ("Operations", "Debug Runbook", "DEBUG_RUNBOOK.md"),
            ("Operations", "UI Smoke Checklist", "UI_SMOKE_CHECKLIST.md"),
            ("Architecture", "Architecture", "ARCHITECTURE.md"),
            ("Architecture", "Decisions", "DECISIONS.md"),
            ("Quality", "Testing Guidelines", "TESTING_GUIDELINES.md"),
            ("Quality", "Service Integration Testing Guide", "SERVICE_INTEGRATION_TESTING_GUIDE.md"),
            ("Planning", "Implementation Tracker", "IMPLEMENTATION_TRACKER.md"),
            ("Planning", "Roadmap", "ROADMAP.md"),
            ("Planning", "V1 Scope", "V1_SCOPE.md"),
        };

        var topics = new List<HelpTopic>();
        foreach (var topic in requestedTopics)
        {
            var fullPath = Path.Combine(docsDirectory, topic.RelativePath);
            if (!File.Exists(fullPath))
            {
                continue;
            }

            topics.Add(new HelpTopic(
                topic.Category,
                topic.Title,
                Path.Combine("docs", topic.RelativePath),
                ReadContentOrFallback(fullPath)));
        }

        return topics;
    }

    private static string? ResolveDocsDirectoryPath()
    {
        var startingPoints = new[] { Environment.CurrentDirectory, AppContext.BaseDirectory };
        foreach (var start in startingPoints)
        {
            var current = new DirectoryInfo(start);
            for (var i = 0; i < 8 && current is not null; i++)
            {
                var docs = Path.Combine(current.FullName, "docs");
                if (Directory.Exists(docs))
                {
                    return docs;
                }

                current = current.Parent;
            }
        }

        return null;
    }

    private static string ReadContentOrFallback(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"Could not load help file: {path}{Environment.NewLine}{Environment.NewLine}{ex.Message}");
        }
    }

    private sealed record HelpTopic(string Category, string Title, string RelativePath, string Content)
    {
        public override string ToString() => Title;
    }
}