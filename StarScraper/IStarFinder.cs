/*
 * Copyright (C) [year]  [name of author]
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 * 
 * 1/27/2024 - Modified by:  imlarry .. added ability to return all star data
 */
using Eleon.Modding;
using static StarScraper.StarFinder;

namespace StarScraper
{
    public interface IStarFinder
    {
        VectorInt3[] Search(VectorInt3 knownPosition);
        StarData[] SearchStarData(VectorInt3 knownPosition);    // NEW .. 1/27/2024
    }
}