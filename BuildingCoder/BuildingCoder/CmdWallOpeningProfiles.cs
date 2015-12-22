﻿#region Header
//
// CmdWallOpeningProfiles.cs - determine and display all wall opening edges including elevation profile lines
//
// Copyright (C) 2015 by Jeremy Tammik, Autodesk Inc. All rights reserved.
//
#endregion // Header

#region Namespaces
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
#endregion // Namespaces

namespace BuildingCoder
{
  [Transaction( TransactionMode.Manual )]
  class CmdWallOpeningProfiles
  {
    static List<PlanarFace> GetWallOpeningPlanarFaces(
      Wall wall,
      ElementId openingId )
    {
      List<PlanarFace> faceList = new List<PlanarFace>();

      List<Solid> solidList = new List<Solid>();

      Options geomOptions = wall.Document.Application.Create.NewGeometryOptions();

      if( geomOptions != null )
      {
        geomOptions.ComputeReferences = true;
        geomOptions.DetailLevel = ViewDetailLevel.Fine;
        geomOptions.IncludeNonVisibleObjects = false;

        GeometryElement geoElem = wall.get_Geometry( geomOptions );

        if( geoElem != null )
        {
          foreach( GeometryObject geomObj in geoElem )
          {
            if( geomObj is Solid )
            {
              solidList.Add( geomObj as Solid );
            }
          }
        }
      }

      foreach( Solid solid in solidList )
      {
        foreach( Face face in solid.Faces )
        {
          if( face is PlanarFace )
          {
            if( wall.GetGeneratingElementIds( face ).Any( x => x == openingId ) )
            {
              faceList.Add( face as PlanarFace );
            }
          }
        }
      }
      return faceList;
    }

    public Result Execute(
      ExternalCommandData commandData,
      ref string message,
      ElementSet elements )
    {
      UIApplication uiapp = commandData.Application;
      UIDocument uidoc = uiapp.ActiveUIDocument;
      Document doc = uidoc.Document;

      Result commandResult = Result.Succeeded;

      try
      {
        UIDocument uiDoc = commandData.Application.ActiveUIDocument;
        Document dbDoc = uiDoc.Document;

        List<ElementId> selectedIds = uiDoc.Selection.GetElementIds().ToList();

        using( Transaction trans = new Transaction( dbDoc ) )
        {
          trans.Start( "Cmd: GetOpeningProfiles" );

          List<ElementId> newIds = new List<ElementId>();

          foreach( ElementId selectedId in selectedIds )
          {
            Wall wall = dbDoc.GetElement( selectedId ) as Wall;

            if( wall != null )
            {
              List<PlanarFace> faceList = new List<PlanarFace>();

              List<ElementId> insertIds = wall.FindInserts( true, false, false, false ).ToList();

              foreach( ElementId insertId in insertIds )
              {
                Element elem = dbDoc.GetElement( insertId );

                if( elem is FamilyInstance )
                {
                  FamilyInstance inst = elem as FamilyInstance;

                  CategoryType catType = inst.Category.CategoryType;
                  Category cat = inst.Category;

                  if( catType == CategoryType.Model && ( cat.Id == dbDoc.Settings.Categories.get_Item( BuiltInCategory.OST_Doors ).Id || cat.Id == dbDoc.Settings.Categories.get_Item( BuiltInCategory.OST_Windows ).Id ) )
                  {
                    faceList.AddRange( GetWallOpeningPlanarFaces( wall, insertId ) );
                  }
                }
                else if( elem is Opening )
                {
                  faceList.AddRange( GetWallOpeningPlanarFaces( wall, insertId ) );
                }
              }

              foreach( PlanarFace face in faceList )
              {
                Plane facePlane = new Plane( face.ComputeNormal( UV.Zero ), face.Origin );
                SketchPlane sketchPlane = SketchPlane.Create( dbDoc, facePlane );

                foreach( CurveLoop curveLoop in face.GetEdgesAsCurveLoops() )
                {
                  foreach( Curve curve in curveLoop )
                  {
                    ModelCurve modelCurve = dbDoc.Create.NewModelCurve( curve, sketchPlane );
                    newIds.Add( modelCurve.Id );
                  }
                }
              }
            }
          }

          if( newIds.Count > 0 )
          {
            View activeView = uiDoc.ActiveGraphicalView;
            activeView.IsolateElementsTemporary( newIds );
          }
          trans.Commit();
        }
      }

      #region Exception Handling

      catch( Autodesk.Revit.Exceptions.ExternalApplicationException e )
      {
        message = e.Message;
        Debug.WriteLine( "Exception Encountered (Application)\n" + e.Message + "\nStack Trace: " + e.StackTrace );

        commandResult = Result.Failed;
      }
      catch( Autodesk.Revit.Exceptions.OperationCanceledException e )
      {
        Debug.WriteLine( "Operation cancelled. " + e.Message );
        message = "Operation cancelled.";

        commandResult = Result.Succeeded;
      }
      catch( Exception e )
      {
        message = e.Message;
        Debug.WriteLine( "Exception Encountered (General)\n" + e.Message + "\nStack Trace: " + e.StackTrace );

        commandResult = Result.Failed;
      }

      #endregion

      return commandResult;
    }
  }
}
