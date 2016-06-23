﻿using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VsTeXCommentsExtension.Integration.Data;
using VsTeXCommentsExtension.Integration.View;

namespace VsTeXCommentsExtension.View
{
    /// <summary>
    /// Interaction logic for TexCommentAdornment.xaml
    /// </summary>
    internal partial class TeXCommentAdornment : UserControl, ITagAdornment, IDisposable
    {
        private readonly List<Span> spansOfChangesFromEditing = new List<Span>();
        private readonly Action<Span> refreshTags;
        private readonly Action<bool> setIsInEditModeForAllAornmentsInDocument;
        private readonly IRenderingManager renderingManager;
        private readonly IWpfTextView textView;

        private TeXCommentTag tag;
        private bool changeMadeWhileInEditMode;
        private bool isInvalidated;

        private bool isInEditMode;
        public bool IsInEditMode
        {
            get { return isInEditMode; }
            set
            {
                Debug.WriteLine($"Adornment {DebugIndex}: IsInEditMode={value}");

                if (changeMadeWhileInEditMode) imageControl.Source = null;

                isInEditMode = value;
                DisplayMode = isInEditMode ? IntraTextAdornmentTaggerDisplayMode.DoNotHideOriginalText : IntraTextAdornmentTaggerDisplayMode.HideOriginalText;
                if (isInEditMode) spansOfChangesFromEditing.Clear();
                changeMadeWhileInEditMode = false;

                SetUpControlsVisibility();

                if (spansOfChangesFromEditing.Count > 0)
                {
                    var resultSpan = spansOfChangesFromEditing[0];
                    for (int i = 1; i < spansOfChangesFromEditing.Count; i++)
                    {
                        var span = spansOfChangesFromEditing[i];
                        resultSpan = new Span(Math.Min(resultSpan.Start, span.Start), Math.Max(resultSpan.End, span.End));
                    }
                    refreshTags(resultSpan);
                }
                else refreshTags(tag.Span);
            }
        }

        public IntraTextAdornmentTaggerDisplayMode DisplayMode { get; private set; }

        private static int debugIndexer;
        public int DebugIndex { get; } = debugIndexer++;

        public LineSpan LineSpan { get; private set; }

        private SolidColorBrush commentsForegroundBrush;
        public SolidColorBrush CommentsForegroundBrush
        {
            get { return commentsForegroundBrush; }
            set
            {
                if (commentsForegroundBrush != value)
                {
                    commentsForegroundBrush = value;
                    leftBorderPanel1.Background = value;
                    leftBorderPanel2.Background = value;
                }
            }
        }

        public TeXCommentAdornment(
            TeXCommentTag tag,
            LineSpan lineSpan,
            SolidColorBrush commentsForegroundBrush,
            IntraTextAdornmentTaggerDisplayMode defaultDisplayMode,
            Action<Span> refreshTags,
            Action<bool> setIsInEditModeForAllAornmentsInDocument,
            IRenderingManager renderingManager,
            IWpfTextView textView)
        {
            ExtensionSettings.Instance.CustomZoomChanged += CustomZoomChanged;

            this.tag = tag;
            this.refreshTags = refreshTags;
            this.setIsInEditModeForAllAornmentsInDocument = setIsInEditModeForAllAornmentsInDocument;
            this.textView = textView;
            this.renderingManager = renderingManager;

            LineSpan = lineSpan;
            DisplayMode = defaultDisplayMode;
            DataContext = this;

            InitializeComponent();

            CommentsForegroundBrush = commentsForegroundBrush;

            isInEditMode = false;
            SetUpControlsVisibility();
            UpdateImageAsync();
        }

        public void Update(TeXCommentTag tag, LineSpan lineSpan)
        {
            LineSpan = lineSpan;

            bool changed = this.tag.Text != tag.Text;
            if (IsInEditMode)
            {
                changeMadeWhileInEditMode = changed;
            }
            else if (changed || isInvalidated)
            {
                this.tag = tag;
                isInvalidated = false;
                UpdateImageAsync();
            }
        }

        public void Invalidate()
        {
            isInvalidated = true;
            imageControl.Source = null;
            SetUpControlsVisibility();
        }

        public void HandleTextBufferChanged(object sender, TextContentChangedEventArgs args)
        {
            if (!IsInEditMode || args.Changes.Count == 0) return;

            var start = args.Changes[0].NewPosition;
            var end = args.Changes[args.Changes.Count - 1].NewEnd;

            spansOfChangesFromEditing.Add(new Span(start, end - start));
        }

        public void Dispose()
        {
            ExtensionSettings.Instance.CustomZoomChanged -= CustomZoomChanged;
        }

        private void UpdateImageAsync()
        {
            imageControl.Source = null;

            renderingManager.LoadContentAsync(tag.GetTextWithoutCommentMarks(), ImageIsReady);
        }

        private void ButtonEdit_Click(object sender, RoutedEventArgs e)
        {
            IsInEditMode = true;
        }

        private void ButtonShow_Click(object sender, RoutedEventArgs e)
        {
            IsInEditMode = false;
        }

        private void ImageIsReady(RendererResult result)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new Action<RendererResult>(ImageIsReady), result);
            }
            else
            {
                var img = result.Image;
                imageControl.Source = img;
                imageControl.Width = img.Width / (textView.ZoomLevel * 0.01);
                imageControl.Height = img.Height / (textView.ZoomLevel * 0.01);
                imageControl.Tag = result.CachePath;
                SetUpControlsVisibility();
            }
        }

        private void SetUpControlsVisibility()
        {
            if (imageControl.Source == null)
            {
                imageControl.Visibility = Visibility.Collapsed;
                progressBar.Visibility = !IsInEditMode ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                imageControl.Visibility = !IsInEditMode ? Visibility.Visible : Visibility.Collapsed;
                progressBar.Visibility = Visibility.Collapsed;
            }

            btnEdit.Visibility = !IsInEditMode ? Visibility.Visible : Visibility.Collapsed;
            btnShow.Visibility = IsInEditMode ? Visibility.Visible : Visibility.Collapsed;
            leftBorderGroupPanel.Visibility = !IsInEditMode ? Visibility.Visible : Visibility.Collapsed;
        }

        private void CustomZoomChanged(double zoomScale)
        {
            var zoomHeader = $"{(int)(100 * zoomScale)}%";
            var zoomMainMenuItem = btnEdit.ContextMenu.Items.Cast<MenuItem>().Single(i => i.Header.ToString() == "Zoom");
            foreach (MenuItem item in zoomMainMenuItem.Items)
            {
                item.IsChecked = item.Header.ToString() == zoomHeader;
            }
        }

        private void MenuItem_EditAll_Click(object sender, RoutedEventArgs e)
        {
            setIsInEditModeForAllAornmentsInDocument(true);
        }

        private void MenuItem_ShowAll_Click(object sender, RoutedEventArgs e)
        {
            setIsInEditModeForAllAornmentsInDocument(false);
        }

        private void MenuItem_OpenImageCache_Click(object sender, RoutedEventArgs e)
        {
            if (imageControl.Source == null || imageControl.Tag == null) return;

            try
            {
                var path = (string)imageControl.Tag;
                var processArgs = $"/e, /select,\"{path}\"";
                Process.Start(new ProcessStartInfo("explorer", processArgs));
            }
            catch { }
        }

        private void MenuItem_ChangeZoom_Click(object sender, RoutedEventArgs e)
        {
            var item = (MenuItem)sender;
            var itemHeader = item.Header.ToString();
            var customZoomScale = 0.01 * int.Parse(itemHeader.Substring(0, itemHeader.Length - 1));

            ExtensionSettings.Instance.CustomZoomScale = customZoomScale; //will trigger zoom changed event
        }
    }
}
