# ONNX Object Detection + License Plate Detection
An object + license plate detection (from a video/image) solution, leveraging Open ALPR (.NET Framework)

| Name | Repository |
| ------ | ------ |
| Open ALPR | [Github Repository](https://github.com/openalpr/openalpr) |

### Getting Started



### Points of Interest

##### MS ML Onnx Object Detection

+ This is a modified version of Microsoft’s samples "OnnxObjectDetectionWPFApp" & "OnnxObjectDetection", leveraging TinyYolo2_model.onnx.
+ There were a few refactors / modifications on the overlay methods, in which it will remove overlapped overlays of identified objects.
+ I looked into adding the duc.onnx model yet was unable to get the bounding boxes correct [Duc](https://github.com/onnx/models/tree/master/vision/object_detection_segmentation/duc).

##### Open ALPR

+ The source has been in a rough state for the last few years based on the issues/comments yet the last public release was close enough to the sample to piece it together.
+ I attempted to rebuild with the latest source yet their build instructions were leveraged for VS15, and after creating a VM with the required dependencies / requirements the build still failed.  (You might have more insight/luck.)
+ I appreciate the effort/time the team put into this, yet I'm giving you a head ups if you investigate their resources further.

Open ALPR requires dependencies not included / built into OpenAlprClient, the files are included on the build:
```
  <ItemGroup>
    <_CopyItems Include="$(SolutionDir)\MlNetOnnxAlpr.OpenAlprClient\Dependencies\**\*.*">
      <InProject>false</InProject>
    </_CopyItems>
  </ItemGroup>
  <Target Name="PostBuild" AfterTargets="PostBuildEvent">
    <Copy SourceFiles="@(_CopyItems)" DestinationFiles="@(_CopyItems->'$(OutDir)\%(RecursiveDir)%(Filename)%(Extension)')" SkipUnchangedFiles="true" OverwriteReadOnlyFiles="true" Retries="3" RetryDelayMilliseconds="300" />
  </Target>
```

##### .NET Core & .NET Framework woes
+ Yes, it's a .NET Core application calling a .NET Framework DLL which is quite bad, yet you could create an API / micro-service to pass the bitmap too and return the relevant information, for a production end-to-end solution.
