# SQLite Storage Architecture (v1.0)

## Goal

Store one PLC scan cycle as one database row.

Benefits:
- Very fast writes
- Small row count
- Suitable for 2000+ tags
- Easy archive rotation
- Self-contained database files

---

## Database File

Database name contains recording start timestamp.

Example:

Project_2026_06_18_12_00_00_123.db

---

## Tables

### Metadata
CREATE TABLE Metadata
(
    Key TEXT PRIMARY KEY,
    Value TEXT NOT NULL
);

INSERT INTO Metadata (Key, Value)
VALUES ('ProjectName', 'BoilerLine1');

INSERT INTO Metadata (Key, Value)
VALUES ('RecordingStartUtc', '2026-06-18T12:00:00.123Z');

Purpose:
- Store recording metadata, Project name and when recording started

Absolute timestamp:

AbsoluteTime = DbStartTime + ElapsedMs

### Tags

CREATE TABLE Tags
(
    TagId INTEGER PRIMARY KEY,
    Name TEXT NOT NULL,
    DataType INTEGER NOT NULL,
    Address TEXT NOT NULL
);

Purpose:
- Store tag definitions
- Store binary layout information
- Allows extraction of values from sample blobs

### Samples

CREATE TABLE Samples
(
    SampleId INTEGER PRIMARY KEY,
    ElapsedMs INTEGER NOT NULL,
    Values BLOB NOT NULL
);

Purpose:
- One row = one PLC scan cycle (2000 tags)
- ElapsedMs relative to database start time
- Values contains all tag values packed into one binary blob

---

## Binary Layout

Values blob contains fixed-size binary data.

Example:

Offset 0   -> Bool MotorRun (1 byte)
Offset 1   -> Bool Alarm (1 byte)
Offset 2   -> Int32 Counter (4 bytes)
Offset 6   -> Float Temperature (4 bytes)

Every sample uses identical layout.

Layout is calculated once when recording starts.

---

## Runtime Flow

PLC
  ↓
Acquisition Service
  ↓
Channel<SampleFrame>
  ↓
Storage Service
  ↓
Batch Buffer
  ↓
SQLite

---

## Sample Model

public sealed class SampleFrame(
    long ElapsedMs,
    byte[] Values
);

One SampleFrame represents one PLC scan. (2000 tags)

---

## Storage Process

1. Read PLC values
2. Serialize values into fixed-size byte[]
3. Create SampleFrame
4. Write SampleFrame to Channel
5. StorageService reads Channel
6. Add to BatchBuffer
7. Save batch to SQLite transaction

---

## Batch Insert

INSERT INTO Samples
(
    ElapsedMs,
    Values
)
VALUES
(
    @elapsedMs,
    @values
);

All inserts executed inside a transaction.

---

## Compression
- LZ4 compression library
- File level compression
- During file rotation, create new active db and forward storage service to use it
- When old db is free, start to compress the file async
- 

## Archive Rotation

Rotation interval:
- 1 hour

Process:
1. Flush BatchBuffer
2. Commit transaction
3. Close database
4. Create new database
5. Write Tags table
6. Continue recording

Each database is fully self-contained.

---

## Query Process

One choice is to read a tag:
1. Read tag definition from Tags table
2. Get Offset and Size
3. Read sample rows
4. Extract bytes from blob
5. Convert bytes to datatype
6. Return trend data

Better solution is to read all at once:
1. Read tag definition from Tags table
2. Get Offset and Size
3. Read whole db
4. Extract bytes from blob
5. Convert bytes to datatype
6. Return trend data
---

## Development Steps

### Step 1
Create TagLayout model.

Contains:
- TagName
- DataType
- Offset
- Size

### Step 2
Build TagLayout when recording starts.

### Step 3
Create binary serializer.

Input:
- PLC values

Output:
- byte[]

### Step 4
Create SampleFrame model.

### Step 5
Change Channel payload to SampleFrame.

### Step 6
Update BatchBuffer to store SampleFrame objects.

### Step 7
Implement SQLite schema:
- Tags
- Samples

### Step 8
Implement batch insert transaction.

### Step 9
Implement 1-hour database rotation.

### Step 10
Implement tag history query engine. Read samples from sqlite to a trend

### Step 11
Benchmark:
- 2000 tags
- 100 ms cycle
- Long duration recording

Measure:
- Insert speed
- Database size
- Memory usage
- Query performance

### Step 12
- Update metrics serviec accordingly

### Step 13
Evaluate future optimizations:
- Compression (LZ4/Zstd)
- TimescaleDB backend
- Change-only storage
- OPC UA support