﻿// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server.NativeInterop;

namespace VsChromium.Server.Search {
  [Export(typeof(ICompiledTextSearchDataFactory))]
  public class CompiledTextSearchDataFactory : ICompiledTextSearchDataFactory {
    private const int MinimumSearchPatternLength = 2;
    private readonly ISearchStringParser _searchStringParser;
    private readonly ICompiledTextSearchProviderFactory _compiledTextSearchProviderFactory;

    [ImportingConstructor]
    public CompiledTextSearchDataFactory(
      ISearchStringParser searchStringParser,
      ICompiledTextSearchProviderFactory compiledTextSearchProviderFactory) {
      _searchStringParser = searchStringParser;
      _compiledTextSearchProviderFactory = compiledTextSearchProviderFactory;
    }

    public CompiledTextSearchData Create(SearchParams searchParams) {
      ParsedSearchString parsedSearchString;
      if (searchParams.Regex) {
        parsedSearchString = new ParsedSearchString(
          new ParsedSearchString.Entry { Text = searchParams.SearchString },
          Enumerable.Empty<ParsedSearchString.Entry>(),
          Enumerable.Empty<ParsedSearchString.Entry>());
      } else {
        parsedSearchString = _searchStringParser.Parse(searchParams.SearchString ?? "");
        // Don't search empty or very small strings -- no significant results.
        //if (string.IsNullOrWhiteSpace(parsedSearchString.MainEntry.Text) ||
        //    (parsedSearchString.MainEntry.Text.Length < MinimumSearchPatternLength)) {
        //  return SearchFileContentsResult.Empty;
        //}
      }

      var searchContentsAlgorithms = CreateSearchAlgorithms(parsedSearchString, searchParams.MatchCase, searchParams.Regex, searchParams.Re2);
      return new CompiledTextSearchData(parsedSearchString, searchContentsAlgorithms);
    }

    private List<ICompiledTextSearchProvider> CreateSearchAlgorithms(
      ParsedSearchString parsedSearchString,
      bool matchCase,
      bool regex,
      bool re2) {
      var searchOptions = NativeMethods.SearchOptions.kNone;
      if (matchCase)
        searchOptions |= NativeMethods.SearchOptions.kMatchCase;
      if (regex)
        searchOptions |= NativeMethods.SearchOptions.kRegex;
      if (re2)
        searchOptions |= NativeMethods.SearchOptions.kRe2Regex;
      return parsedSearchString.EntriesBeforeMainEntry
        .Concat(new[] { parsedSearchString.MainEntry })
        .Concat(parsedSearchString.EntriesAfterMainEntry)
        .OrderBy(x => x.Index)
        .Select(entry => _compiledTextSearchProviderFactory.CreateProvider(entry.Text, searchOptions))
        .ToList();
    }  
  }
}