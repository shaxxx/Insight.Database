﻿using System.Data;
using System.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Insight.Database;
using Microsoft.SqlServer.Types;
using NUnit.Framework;
using Insight.Tests.Cases;
using Insight.Database.Structure;

#pragma warning disable 0649

namespace Insight.Tests
{
	[TestFixture]
	public class StructureTest : BaseTest
	{
		#region Default Recordset Test
		[Recordset(typeof(HasDefaultRecordset), typeof(Beer))]
		class HasDefaultRecordset
		{
			public int ID;
			public Beer Beer;
		}

		[Test]
		public void RecordsetAttributeShouldControlDefaultOneToOneMapping()
		{
			var result = Connection().SingleSql<HasDefaultRecordset>("SELECT ID=1, ID=2");
			Assert.AreEqual(1, result.ID);
			Assert.IsNotNull(result.Beer);
			Assert.AreEqual(2, result.Beer.ID);
		}
		#endregion

		#region Default ID Test
		class HasDefaultID
		{
			[RecordId]
			public int Foo;
			public IList<Beer> List;
		}

		[Test]
		public void RecordIDAttributeShouldDefineParentID()
		{
			var result = Connection().QuerySql("SELECT Foo=1; SELECT ParentID=1, ID=2", null,
				Query.Returns(Some<HasDefaultID>.Records)
						.ThenChildren(Some<Beer>.Records))
						.First();

			Assert.AreEqual(1, result.Foo);
			Assert.AreEqual(1, result.List.Count);
			Assert.AreEqual(2, result.List[0].ID);
		}
		#endregion

		#region Default List Test
		class HasDefaultList
		{
			public int ID;

			[ChildRecords]
			public IList<Beer> List;

			// normally this would throw with two lists of beer.
			public IList<Beer> NotList;
		}

		[Test]
		public void ChildRecordsAttributeShouldDefineList()
		{
			var result = Connection().QuerySql("SELECT ID=1; SELECT ParentID=1, ID=2", null,
				Query.Returns(Some<HasDefaultList>.Records)
						.ThenChildren(Some<Beer>.Records))
						.First();

			Assert.AreEqual(1, result.ID);
			Assert.AreEqual(1, result.List.Count);
			Assert.AreEqual(2, result.List[0].ID);
		}
		#endregion

		#region Read-Only List Test
		class HasReadOnlyList
		{
			public int ID;

			public IList<Beer> List { get { return _list; } }
			private readonly List<Beer> _list = new List<Beer>();
		}

		[Test, ExpectedException(typeof(InvalidOperationException))]
		public void ReadOnlyListShouldThrowException()
		{
			var result = Connection().QuerySql("SELECT ID=1; SELECT ParentID=1, ID=2", null,
				Query.Returns(Some<HasReadOnlyList>.Records)
						.ThenChildren(Some<Beer>.Records))
						.First();

			Assert.AreEqual(1, result.ID);
			Assert.AreEqual(1, result.List.Count);
			Assert.AreEqual(2, result.List[0].ID);
		}
		#endregion

		#region Inline Column Overrides
		[Test]
		public void CanOverrideColumnsInline()
		{
			var mappedRecords = new OneToOne<HasDefaultRecordset>(
				new ColumnOverride<HasDefaultRecordset>("Foo", "ID"));

			var result = Connection().QuerySql(
				"SELECT Foo=1, ID=2",
				null,
				Query.Returns(mappedRecords)).First();

			Assert.AreEqual(1, result.ID);
			Assert.AreEqual(2, result.Beer.ID);
		}
		#endregion

		#region Multi-Class Reader Tests
		class MyClass
		{
		}

		class MyClassA : MyClass
		{
			public int A;
		}

		class MyClassB : MyClass
		{
			public int B;
		}

#if !NET35
		[Test]
		public void MultiReaderCanDeserializeDifferentClasses()
		{
			var mr = new MultiReader<MyClass>(
				reader =>
				{
					switch ((string)reader["Type"])
					{
						default:
						case "a":
							return OneToOne<MyClassA>.Records;
						case "b":
							return OneToOne<MyClassB>.Records;
					}
				});

			var results = Connection().QuerySql("SELECT [Type]='a', A=1, B=NULL UNION SELECT [Type]='b', A=NULL, B=2", Parameters.Empty, Query.Returns(mr));

			Assert.AreEqual(2, results.Count);
			Assert.IsTrue(results[0] is MyClassA);
			Assert.AreEqual(1, ((MyClassA)results[0]).A);
			Assert.IsTrue(results[1] is MyClassB);
			Assert.AreEqual(2, ((MyClassB)results[1]).B);
		}
#endif

		[Test]
		public void PostProcessCanReadFieldsInAnyOrder()
		{
			var pr = new PostProcessRecordReader<MyClassA>(
				(reader, a) =>
				{
					if (reader["Type"].ToString() == "a")
						a.A = 9;
					return a;
				});

			var results = Connection().QuerySql("SELECT [Type]='a', A=1, B=NULL", Parameters.Empty, Query.Returns(pr));

			Assert.AreEqual(1, results.Count);
			Assert.IsTrue(results[0] is MyClassA);
			Assert.AreEqual(9, ((MyClassA)results[0]).A);
		}
		#endregion

		#region Issue 109 Tests
		public class ParentFor109
		{
			public int ID;
			public List<ChildFor109> Children;
		}

		public class ChildFor109
		{
			public int Foo;
		}

		[Test]
		public void ColumnMappingShouldWorkForChildRecordsets()
		{
			var childReader = new OneToOne<ChildFor109>(new ColumnOverride("Bar", "Foo"));

			var results = Connection().QuerySql("SELECT ID=1; SELECT ParentID=1, Bar=3", Parameters.Empty,
							Query.Returns(Some<ParentFor109>.Records)
								.ThenChildren(childReader)).First();

			Assert.AreEqual(1, results.Children.Count);
			Assert.AreEqual(3, results.Children[0].Foo);
		}
		#endregion
	}
}