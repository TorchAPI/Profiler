def test_with_torch(branch)
{
	try {
		stage('Acquire Torch ' + branch) {
			bat 'IF EXIST TorchBinaries RMDIR /S /Q TorchBinaries'
			bat 'mkdir TorchBinaries'
			step([$class: 'CopyArtifact', projectName: "Torch/${branch}", filter: "**/Torch*.dll", flatten: true, fingerprintArtifacts: true, target: "TorchBinaries"])
			step([$class: 'CopyArtifact', projectName: "Torch/${branch}", filter: "**/Torch*.exe", flatten: true, fingerprintArtifacts: true, target: "TorchBinaries"])
		}

		stage('Build + Torch ' + branch) {
			currentBuild.description = bat(returnStdout: true, script: '@powershell -File Versioning/version.ps1').trim()
			bat "IF EXIST \"bin\" rmdir /Q /S \"bin\""
			bat "IF EXIST \"bin-test\" rmdir /Q /S \"bin-test\""
			bat "\"${tool 'MSBuild'}msbuild\" Profiler.sln /p:Configuration=${buildMode} /p:Platform=x64 /t:Clean"
			bat "\"${tool 'MSBuild'}msbuild\" Profiler.sln /p:Configuration=${buildMode} /p:Platform=x64"
		}

	
		stage('Test + Torch ' + branch) {
			
		}

		return true
	} catch (e) {
		return false
	}
}

node('windows') {
	stage('Checkout') {
		checkout scm
		bat 'git pull https://github.com/TorchAPI/Profiler/ master --tags'
	}

	stage('Acquire SE') {
		bat 'powershell -File Jenkins/jenkins-grab-se.ps1'
		bat 'IF EXIST GameBinaries RMDIR GameBinaries'
		bat 'mklink /J GameBinaries "C:/Steam/Data/DedicatedServer64/"'
	}

	stage('Acquire NuGet Packages') {
		bat 'cd C:\\Program Files\\Jenkins'
		bat '"C:\\Program Files\\Jenkins\\nuget.exe" restore Profiler.sln'
	}
	
	if (env.BRANCH_NAME == "master") {
		buildMode = "Release"
	} else {
		buildMode = "Debug"
	}
	result = test_with_torch("master")
	if (result) {
		currentBuild.result = "SUCCESS"
		stage('Archive') {
			archiveArtifacts artifacts: "bin/x64/${buildMode}/Profiler.*", caseSensitive: false, fingerprint: true, onlyIfSuccessful: true

			zipFile = "bin\\profiler.zip"
			packageDir = "bin\\profiler\\"

			bat "IF EXIST ${zipFile} DEL ${zipFile}"
			bat "IF EXIST ${packageDir} RMDIR /S /Q ${packageDir}"

			bat "xcopy bin\\x64\\${buildMode}\\Profiler.* ${packageDir}"
			powershell "(Get-Content manifest.xml).Replace('\${VERSION}', [System.Diagnostics.FileVersionInfo]::GetVersionInfo(\"\$PWD\\${packageDir}Profiler.dll\").ProductVersion) | Set-Content \"${packageDir}/manifest.xml\""
			powershell "Add-Type -Assembly System.IO.Compression.FileSystem; [System.IO.Compression.ZipFile]::CreateFromDirectory(\"\$PWD\\${packageDir}\", \"\$PWD\\${zipFile}\")"
			archiveArtifacts artifacts: zipFile, caseSensitive: false, onlyIfSuccessful: true
		}
	}
	else
		currentBuild.result = "FAIL"
}
