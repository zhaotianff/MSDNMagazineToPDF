﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Prism.Commands;
using Prism;
using System.Collections.ObjectModel;
using Prism.Mvvm;
using System.Windows;
using System.Windows.Controls;
using CefSharp;

using MSDN_Magazine_To_PDF.Util;
using MSDN_Magazine_To_PDF.Model;

using static MSDN_Magazine_To_PDF.Util.ResourceProc;
using System.Globalization;

namespace MSDN_Magazine_To_PDF.ViewModels
{
    public class MainWindowViewModel : BindableBase
    {
        #region Field
        private ObservableCollection<Magazine> magazineList = new ObservableCollection<Magazine>();
        private ObservableCollection<System.Windows.Controls.Expander> yearList = new ObservableCollection<System.Windows.Controls.Expander>();
        private string cc = "";    
        #endregion

        #region Const
        private const string OVERVIEW_KEYWORD = "Overview";
        private const string CONNECT_KEYWORD = "Connect";
        #endregion

        #region Property
        private CefSharp.Wpf.ChromiumWebBrowser ChromiumWebBrowser { get; set; }

        private CultureInfo CurrentCulture { get; set; } = CultureInfo.CurrentCulture;

        public ObservableCollection<Magazine> MagazineList
        {
            get => magazineList;
            set => SetProperty(ref magazineList, value, "MagazineList");
        }

        public ObservableCollection<System.Windows.Controls.Expander> YearList
        {
            get => yearList;
            set => SetProperty(ref yearList, value, "YearList");
        }

        public string CC
        {
            get => cc;
            set => SetProperty(ref cc,value,"CC");
        }
        #endregion

        #region Command
        public DelegateCommand<object> SelectMagazineCommand { get; private set; }
        public DelegateCommand<CefSharp.Wpf.ChromiumWebBrowser> LoadedCommand { get; private set; }
        #endregion

        public MainWindowViewModel()
        {
            LoadMagazineList();

            SelectMagazineCommand = new DelegateCommand<object>(SelectMagazine);
            LoadedCommand = new DelegateCommand<CefSharp.Wpf.ChromiumWebBrowser>(Loaded);
        }

        private async void LoadMagazineList()
        {
            var json = await WebUtil.GetHtml(Urls.GetListUrl);
            var magazineRoot = JsonUtil.Deserialize<MagazineRoot>(json);

            foreach (var item in magazineRoot.items.First()?.children)
            {
                ListBox listBox = new ListBox();
                listBox.Style = TryFindLocalStyle("ListBoxStyle");
                listBox.SelectionChanged += MonthListBox_SelectionChanged;

                foreach (var month in item.children)
                {
                    if (month.toc_title == OVERVIEW_KEYWORD || month.toc_title == CONNECT_KEYWORD)
                        continue;

                    listBox.Items.Add(new MonthItem() { Title = month.toc_title, Url = month.href });
                }

                YearList.Add(new Expander() { Header = item.toc_title,Style = (Style)Application.Current.FindResource("ExpanderStyle"),Content = listBox });
            }
        }

        private async void MonthListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = e.AddedItems[0] as MonthItem;
            var url = Urls.BaseUrl + selectedItem.Url;
            var html = await WebUtil.GetHtml(url);
            ClearMagazines();
            ExtractMagazines(html,selectedItem.Url);
        }

        private void ClearMagazines()
        {
            MagazineList.Clear();
        }

        private void ExtractMagazines(string html, string url)
        {
            //2013及以前的年份网页格式不一样，需要另外处理
            //通过接口获取出来的只有标题，没有描述及作者
            if (RegexUtil.ExtractNumber(url) <= 2013)
            {
                ExtractMagazinesBefore2013(html, url);
            }
            else
            {
                ExtractMagazineAfter2013(html, url);
            }
        }

        private async void ExtractMagazineAfter2013(string html,string url)
        {
            var table = await AngleSharpHelper.CssSelectorParse("table", html);
            var magazines = await AngleSharpHelper.CssSelectorParse("td", table.First().OuterHtml);

            foreach (var item in magazines)
            {
                var magazine = new Magazine();
                var pEle = await AngleSharpHelper.CssSelectorParse("p", item.OuterHtml);

                if (pEle.Length != 2)
                    continue;

                magazine.Author = pEle.ElementAt(0).TextContent;
                magazine.Description = pEle.ElementAt(1).TextContent;

                var imgEle = await AngleSharpHelper.CssSelectorParse("img", item.OuterHtml);
                if (imgEle != null)
                {
                    magazine.CoverUrl = Urls.BaseUrl + url + "/" + imgEle.ElementAt(0).Attributes["src"].Value;
                }

                var h2Ele = await AngleSharpHelper.CssSelectorParseSingle("h2", item.OuterHtml);
                if (h2Ele != null)
                {
                    magazine.Title = h2Ele.TextContent;
                }

                var aEle = await AngleSharpHelper.CssSelectorParseSingle("a", item.OuterHtml);
                if (aEle != null)
                {
                    magazine.LinkUrl = Urls.BaseUrl + url + "/" + aEle.Attributes["href"].Value;
                }

                MagazineList.Add(magazine);
            }
        }

        private async void ExtractMagazinesBefore2013(string html,string url)
        {
            var main = await AngleSharpHelper.CssSelectorParseSingle("#main", html);
            var magazines = await AngleSharpHelper.CssSelectorParse("p", main.OuterHtml);

        }

        private void Loaded(CefSharp.Wpf.ChromiumWebBrowser chromiumWebBrowser)
        {
            ChromiumWebBrowser = chromiumWebBrowser;
        }

        private async void SelectMagazine(object magazineObj)
        {
            if(magazineObj != null && magazineObj is Magazine magazine)
            {
                var html = await WebUtil.GetHtml(magazine.LinkUrl);
                var main = await AngleSharpHelper.CssSelectorParseSingle("#main", html);

                ChromiumWebBrowser?.LoadHtml(main.OuterHtml);
            }
        }
    }
}
