/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
*/

using NUnit.Framework;
using Python.Runtime;
using QuantConnect.Securities;
using QuantConnect.Util;
using System;
using System.Collections.Generic;
using System.Linq;

namespace QuantConnect.Tests.Python
{
    [TestFixture]
    public partial class PandasConverterTests
    {
        [Test, TestCaseSource(nameof(TestDataFrameParameterlessFunctions))]
        public void BackwardsCompatibilityDataFrameParameterlessFunctions(string method, string index, bool cache)
        {
            if (cache) SymbolCache.Set("SPY", Symbols.SPY);

            using (Py.GIL())
            {
                dynamic test = PythonEngine.ModuleFromString("testModule",
                    $@"
def Test(df, symbol):
    df = df.lastprice.unstack(level=0).{method}()
    # If not DataFrame, return
    if not hasattr(df, 'columns'):
        return
    if df.iloc[-1][{index}] is 0:
        raise Exception('Data is zero')").GetAttr("Test");

                Assert.DoesNotThrow(() => test(GetTestDataFrame(Symbols.SPY), Symbols.SPY));
            }
        }

        [Test, TestCaseSource(nameof(TestSeriesParameterlessFunctions))]
        public void BackwardsCompatibilitySeriesParameterlessFunctions(string method, string index, bool cache)
        {
            if (cache) SymbolCache.Set("SPY", Symbols.SPY);

            using (Py.GIL())
            {
                dynamic test = PythonEngine.ModuleFromString("testModule",
                    $@"
def Test(df, symbol):
    series = df.lastprice
    series = series.{method}()
    # If not Series, return
    if not hasattr(series, 'index') or type(series) is tuple:
        return
    if series.loc[{index}].iloc[-1] is 0:
        raise Exception('Data is zero')").GetAttr("Test");

                Assert.DoesNotThrow(() => test(GetTestDataFrame(Symbols.SPY), Symbols.SPY));
            }
        }

        private static TestCaseData[] TestDataFrameParameterlessFunctions => _parameterlessFunctions["DataFrame"];

        private static TestCaseData[] TestSeriesParameterlessFunctions => _parameterlessFunctions["Series"];

        private static Dictionary<string, TestCaseData[]> _parameterlessFunctions = GetParameterlessFunctions();

        private static Dictionary<string, TestCaseData[]> GetParameterlessFunctions()
        {
            var functionsByType = new Dictionary<string, TestCaseData[]>();

            using (Py.GIL())
            {
                var module = PythonEngine.ModuleFromString("Test",
                    @"import pandas
from inspect import getmembers, isfunction, signature

skipped = [ 'boxplot', 'hist', 'plot',    # <- Graphics

    'bool', 'drop', 'ewm', 'fillna', 'filter', 'groupby', 'melt', 'pivot', 'pivot_table', 'rename',
    'select_dtypes', 'slice_shift', 'swaplevel', 'to_period', 'to_sparse', 'to_timestamp', 'tshift',

    'describe', 'get_dtype_counts', 'get_ftype_counts', 'mode', 'reset_index', 'slice_shift',
    'to_json', 'to_latex', 'to_list', 'to_msgpack', 'to_string', 'tolist', 'value_counts'
]

def getFunctions(cls):
    functions = list()
    for name, member in getmembers(cls):
        if isfunction(member) and not name.startswith('_') and name not in skipped:
            s = signature(member)
            parameters = s.parameters
            count = 0
            for parameter in parameters.values():
                if parameter.default is parameter.empty:
                    count += 1
                else:
                    break
            if count < 2:
                functions.append(name)
    return functions

DataFrame = getFunctions(pandas.DataFrame)
Series = getFunctions(pandas.Series)
");
                Func<string, TestCaseData[]> converter = s =>
                {
                    var list = (List<string>)module.GetAttr(s).AsManagedObject(typeof(List<string>));
                    return list.SelectMany(x => new[]
                    {
                        new TestCaseData(x, "'SPY'", true),
                        new TestCaseData(x, "symbol", false),
                        new TestCaseData(x, "str(symbol.ID)", false)
                    }
                    ).ToArray();
                };

                functionsByType.Add("DataFrame", converter("DataFrame"));
                functionsByType.Add("Series", converter("Series"));
            }

            return functionsByType;
        }
    }
}