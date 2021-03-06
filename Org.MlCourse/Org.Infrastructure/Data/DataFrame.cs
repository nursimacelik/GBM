﻿using Org.Infrastructure.Collections;
using Org.Infrastructure.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Org.Infrastructure.Data
{
    public class DataFrame
    {
        private IDictionary<string, Bin> _binCollection;
        private Bijection<string, int> _columnOrder;

        public DataColumnCollection _columnCollection;

        //POST-BINNING
        private IDictionary<string, int[]> _integerFrame;

        //POST-READ
        private IDictionary<string, List<int>> _rawCategorical;
        private IDictionary<string, List<float>> _rawNumerical;

        private IDictionary<string, Action<string, string>> _actions;

        private IBlas _blas;
        public void Initialize(DataColumnCollection collection, int capacity)
        {
            _columnCollection = collection;
            _binCollection = new Dictionary<string, Bin>();
            _rawCategorical = new Dictionary<string, List<int>>();
            _rawNumerical = new Dictionary<string, List<float>>();

            _actions = new Dictionary<string, Action<string, string>>();
            foreach (var column in _columnCollection.Values)
            {
                var name = column.Name;
                //var order = column.Order;
                //_columnOrder.Add(name, order);
                if (column.MeasurementType == ColumnMeasurementType.Categorical)
                {
                    _rawCategorical.Add(name, new List<int>(capacity));
                    _binCollection.Add(column.Name, new CategoricalBin(column.MissingNominalValues));
                    _actions.Add(name, AddCategorical);
                }
                else if (column.MeasurementType == ColumnMeasurementType.Numerical)
                {
                    _rawNumerical.Add(name, new List<float>(capacity));
                    _actions.Add(name, AddFloat);
                }
            }
        }
        public void Add(int order, string s)
        {
            var name = _columnOrder[order];
            _actions[name](name, s);
        }

        private void AddCategorical(string name, string s)
        {
            var bin = (CategoricalBin)_binCollection[name];
            bin.Add(s);
            var idx = bin.GetIndex(s);
            _rawCategorical[name].Add(idx);
        }

        private void AddFloat(string name, string s)
        {
            var f = Single.NaN;
            var flag = Single.TryParse(s, out f);
            _rawNumerical[name].Add(flag ? f : Single.NaN);
        }

        public void SetBlas(IBlas blas)
        {
            _blas = blas;
        }
        public void CreateCategoricalBins()
        {
            var categoricalKeys = _rawCategorical.Keys.ToList();
            _integerFrame = new Dictionary<string, int[]>();
            foreach (var column in categoricalKeys)
            {
                var list = _rawCategorical[column];
                _integerFrame.Add(column, list.ToArray());
                _rawCategorical.Remove(column);
            }
        }

        public void CreateNumericalBins(int maxBins)
        {
            if (_integerFrame == null)
                _integerFrame = new Dictionary<string, int[]>();



            var helper = new StatsFunctions(_blas);
            foreach (var item in _rawNumerical)
            {
                var name = item.Key;
                var src = item.Value;
                var thresholds = helper.GetQuantiles(src, maxBins);
                if (thresholds == null) continue;
                var bin = new NumericalBin(thresholds);
                var dest = new int[src.Count];
                for (int i = 0; i < src.Count; i++)
                {
                    dest[i] = bin.GetIndex(src[i]);
                }
                _binCollection.Add(name, bin);
                _integerFrame.Add(name, dest);
            }
        }
    }
}
