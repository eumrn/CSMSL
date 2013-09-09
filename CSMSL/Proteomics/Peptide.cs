﻿///////////////////////////////////////////////////////////////////////////
//  Peptide.cs - An amino acid residue that is the child of a protein     /
//                                                                        /
//  Copyright 2012 Derek J. Bailey                                        /
//  This file is part of CSMSL.                                           /
//                                                                        /
//  CSMSL is free software: you can redistribute it and/or modify         /
//  it under the terms of the GNU General Public License as published by  /
//  the Free Software Foundation, either version 3 of the License, or     /
//  (at your option) any later version.                                   /
//                                                                        /
//  CSMSL is distributed in the hope that it will be useful,              /
//  but WITHOUT ANY WARRANTY; without even the implied warranty of        /
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the         /
//  GNU General Public License for more details.                          /
//                                                                        /
//  You should have received a copy of the GNU General Public License     /
//  along with CSMSL.  If not, see <http://www.gnu.org/licenses/>.        /
///////////////////////////////////////////////////////////////////////////


using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Statistics;

namespace CSMSL.Proteomics
{
    public class Peptide : AminoAcidPolymer
    {
        public int StartResidue { get; set; }

        public int EndResidue { get; set; }

        public AminoAcidPolymer Parent { get; set; }

        public Peptide()
        {
            Parent = null;
            StartResidue = 0;
            EndResidue = 0;
        }

        public Peptide(AminoAcidPolymer aminoAcidPolymer, bool includeModifications = true)
            : base(aminoAcidPolymer, includeModifications)
        {
            Parent = aminoAcidPolymer;
            StartResidue = 0;
            EndResidue = StartResidue + Length - 1;
        }

        public Peptide(AminoAcidPolymer aminoAcidPolymer, int firstResidue, int length, bool includeModifications = true)
            : base(aminoAcidPolymer, firstResidue, length, includeModifications)
        {
            Parent = aminoAcidPolymer;
            StartResidue = firstResidue;
            EndResidue = firstResidue + length - 1;
        }

        public Peptide(AminoAcidPolymer aminoAcidPolymer)
            : this(aminoAcidPolymer, 0, aminoAcidPolymer.Length) { }
                 
        public Peptide(string sequence)
            : this(sequence, null, 0) { }

        public Peptide(string sequence, Protein parent)
            : this(sequence, parent, 0) { }

        public Peptide(string sequence, Protein parent, int startResidue)
            : base(sequence)
        {
            Parent = parent;
            StartResidue = startResidue;
            EndResidue = startResidue + Length - 1;
        }

        public IEnumerable<Peptide> GenerateIsoforms(params Modification[] modifications)
        {
            return GenerateIsoforms(this, modifications); // Just call the static method
        }

        public Peptide GetSubPeptide(int firstResidue, int length)
        {
            return new Peptide(this, firstResidue, length);
        }

        public new bool Equals(AminoAcidPolymer other)
        {
            return base.Equals(other);
        }

        public static IEnumerable<Peptide> GenerateIsoforms(Peptide peptide, params Modification[] modifications)
        {
            // Get the number of modifications to this peptide
            int numberOfModifications = modifications.Length;

            if (numberOfModifications < 1)
            {
                // No modifications, return the base peptide               
                yield return peptide;
            }
            else if (numberOfModifications == 1)
            {
                // Only one modification, use the faster algorithm
                foreach (Peptide pep in GenerateIsoforms(peptide, modifications[0], 1))
                {
                    yield return pep;
                }
            }
            else
            {
                // More than one ptm case

                // Get all the unique modified sites for all the mods
                Dictionary<Modification, List<int>> allowedSites = new Dictionary<Modification, List<int>>();
                List<int> sites = null;
                foreach (Modification mod in modifications)
                {
                    if (!allowedSites.TryGetValue(mod, out sites))
                    {
                        allowedSites.Add(mod, mod.GetSites(peptide).ToList());
                    }
                }

                // Only one type of mod, use the faster algorithm
                if (allowedSites.Count == 1)
                {
                    foreach (Peptide pep in GenerateIsoforms(peptide, modifications[0], numberOfModifications))
                    {
                        yield return pep;
                    }
                    yield break;
                }

                HashSet<Modification[]> results = new HashSet<Modification[]>(new ModificationArrayComparer());

                // Call out to the recursive helper method, starting with mod 0 and site 0
                GenIsoHelper(results, new Modification[peptide.Length+2], modifications, allowedSites, 0, 0);

                // Create correct peptide mappings
                foreach (Modification[] modArray in results)
                {
                    Peptide pep = new Peptide(peptide);
                    for (int i = 0; i < modArray.Length; i++)
                    {
                        var mod = modArray[i];
                        if(mod == null)
                            continue;

                        if (i == 0)
                        {
                            pep.NTerminusModification = mod;
                        } else if (i == peptide.Length)
                        {
                            pep.CTerminusModification = mod;
                        }
                        else
                        {
                            pep.SetModification(mod, i);
                        }

                    }
                    yield return pep;
                }
            }
        }

        private static Modification[] GenIsoHelper(HashSet<Modification[]> results, Modification[] modArray, Modification[] mods, Dictionary<Modification, List<int>> allowedSites, int mod_index, int site_index)
        {
            if (mod_index >= mods.Count())
            {
                return modArray; // Out of mods
            }

            // Get the current mod under consideration
            Modification currentMod = mods[mod_index];

            // Retrieve the list of sites that it can modify
            List<int> sites = allowedSites[currentMod];

            while (site_index < sites.Count())
            {
                // Get the index to the peptide where the mod would occur
                int index = sites[site_index];

                // Check to see if this site is already modded
                if (modArray[index] == null)
                {
                    // Set the current mod to this site
                    modArray[index] = currentMod;

                    // Check to see if there are any more mods
                    if (mod_index < mods.Count() - 1)
                    {
                        // Still have more mods to add so start so the new mod at the beginning of it's sites
                        Modification[] templist = GenIsoHelper(results, modArray, mods, allowedSites, ++mod_index, 0);

                        // All done for this master level, go up a level
                        mod_index--;

                        // Create a deep-copy clone
                        Array.Copy(templist, modArray, templist.Length);

                        // Remove the last mod added
                        modArray[index] = null;
                    }
                    else
                    {
                        // Completed all the mods, add the configuration to the saved list, if possible
                        results.Add((Modification[])modArray.Clone());
                        
                        // Remove the last mod added
                        modArray[index] = null;
                    }
                }

                // Go to the next site for this mod  
                site_index++;
            }

            // All Done with this level
            return modArray;
        }
        

        public static IEnumerable<Peptide> GenerateIsoforms(Peptide peptide, Modification modification, long ptms)
        {
            // Get all the possible modified-residues' indices (zero-based)
            List<int> sites = modification.GetSites(peptide).ToList();

            // Total number of PTM sites
            int ptmsites = sites.Count;

            // Exact number of possible isoforms
            long isoforms = Util.Combinatorics.BinomCoefficient(ptmsites, ptms);

            // For each possible isoform
            for (long isoform = 0; isoform < isoforms; isoform++)
            {
                // Create a new peptide based on the one passed in
                Peptide pep = new Peptide(peptide, false);

                long x = isoforms - isoform - 1;
                long a = ptmsites;
                long b = ptms;

                // For each ptm
                for (int i = 0; i < ptms; i++)
                {
                    long ans = Util.Combinatorics.LargestV(a, b, x);
                    int index = (int)sites[(int)(ptmsites - ans - 1)];
                    if (index == 0)
                    {
                        pep.NTerminusModification = modification;
                    } else if (index == pep.Length)
                    {
                        pep.CTerminusModification = modification;
                    }
                    else
                    {
                        pep.SetModification(modification, index);
                    }
                    x -= Util.Combinatorics.BinomCoefficient(ans, b);
                    a = ans;
                    b--;
                }

                yield return pep;
            }

            // All done!
            yield break;
        }
    }

    class ModificationArrayComparer : IEqualityComparer<Modification[]>
    {
        public bool Equals(Modification[] x, Modification[] y)
        {
            int length = x.Length;
            if (length != y.Length)
                return false;
            for (int i = 0; i < length; i++)
                if (x[i] != y[i]) return false;
            return true;
        }

        public int GetHashCode(Modification[] obj)
        {
            unchecked
            {
                const int p = 16777619;
                int hash = obj.Where(t => t != null).Aggregate((int) 2166136261, (current, t) => (current ^ t.GetHashCode())*p);
                hash += hash << 13;
                hash ^= hash >> 7;
                hash += hash << 3;
                hash ^= hash >> 17;
                hash += hash << 5;
                return hash;
            }
        }
    }
}