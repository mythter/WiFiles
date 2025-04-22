namespace Client.Helpers
{
	public static class FileHelper
	{
		public static string GetUniqueFilePath(string fileName, string directory)
		{
			string extension = Path.GetExtension(fileName);
			string tempName = Path.GetFileNameWithoutExtension(fileName);
			string filePath = Path.Combine(directory, fileName);
			int n = 1;
			while (File.Exists(filePath))
			{
				fileName = $"{tempName} ({n}){extension}";
				filePath = Path.Combine(directory, fileName);
				n++;
			}

			return filePath;
		}

		public static int GetBufferSizeByFileSize(long fileSize)
		{
			return fileSize switch
			{                             // file size is:
				< 10_485_760  => 1024,    // less than 10 MB
				< 104_857_600 => 4096,    // less than 100 MB
				_             => 16384    // more or equal 100 MB
			};
		}

		public static void DeleteFileIfExists(string? filePath)
		{
			if (File.Exists(filePath))
			{
				File.Delete(filePath);
			}
		}

		public static string GetPhosphorFileIcon(string filePath)
		{
			// useful repository with file extensions
			// https://github.com/dyne/file-extension-list

			return Path.GetExtension(filePath)?.Trim('.')?.ToLower() switch
			{
				// text formats
				"pdf" => "<i class=\"ph ph-file-pdf\"></i>",
				"docx" or
				"doc" => "<i class=\"ph ph-file-doc\"></i>",
				"csv" => "<i class=\"ph ph-file-csv\"></i>",
				"txt" => "<i class=\"ph ph-file-txt\"></i>",
				"md" => "<i class=\"ph ph-file-md\"></i>",
				"ebook" or
				"log" or
				"msg" or
				"odt" or
				"org" or
				"pages" or
				"pdf" or
				"rtf" or
				"rst" or
				"tex" or
				"wpd" or
				"wps" => "<i class=\"ph ph-file-text\"></i>",
				// code
				"css" => "<i class=\"ph ph-file-css\"></i>",
				"html" => "<i class=\"ph ph-file-html\"></i>",
				"js" => "<i class=\"ph ph-file-js\"></i>",
				"jsx" => "<i class=\"ph ph-file-jsx\"></i>",
				"rs" => "<i class=\"ph ph-file-rs\"></i>",
				"sql" => "<i class=\"ph ph-file-sql\"></i>",
				"ts" => "<i class=\"ph ph-file-ts\"></i>",
				"tsx" => "<i class=\"ph ph-file-tsx\"></i>",
				"vue" => "<i class=\"ph ph-file-vue\"></i>",
				"cs" => "<i class=\"ph ph-file-c-sharp\"></i>",
				"c" => "<i class=\"ph ph-file-c\"></i>",
				"cpp" => "<i class=\"ph ph-file-cpp\"></i>",
				"py" => "<i class=\"ph ph-file-py\"></i>",
				"ini" => "<i class=\"ph ph-file-ini\"></i>",
				"h" or
				"hpp" or
				"sh" or
				"bash" or
				"zsh" or
				"ps1" or
				"rb" or
				"ada" or
				"adb" or
				"ads" or
				"asm" or
				"asp" or
				"aspx" or
				"bas" or
				"bat " or
				"cbl" or
				"cc" or
				"class" or
				"clj" or
				"cob" or
				"csh" or
				"cxx" or
				"d" or
				"diff" or
				"e" or
				"el" or
				"f" or
				"f77" or
				"f90" or
				"fish" or
				"for" or
				"fth" or
				"ftn" or
				"go" or
				"groovy" or
				"h" or
				"hh" or
				"hpp" or
				"hs" or
				"htm" or
				"hxx" or
				"inc" or
				"java" or
				"json" or
				"jsp" or
				"ksh" or
				"kt" or
				"kts" or
				"lhs" or
				"lisp" or
				"lua" or
				"m" or
				"m4" or
				"nim" or
				"patch" or
				"php" or
				"php3" or
				"php4" or
				"php5" or
				"phtml" or
				"pl" or
				"po" or
				"pp" or
				"prql" or
				"r" or
				"rb" or
				"s" or
				"scala" or
				"sh" or
				"swg" or
				"swift" or
				"v" or
				"vb" or
				"vcxproj" or
				"xcodeproj" or
				"xml" or
				"zig" or
				"zsh" => "<i class=\"ph ph-file-code\"></i>",
				// images
				"png" => "<i class=\"ph ph-file-png\"></i>",
				"jpeg" or
				"jfif" or
				"pjpeg" or
				"pjp" or
				"jpg" => "<i class=\"ph ph-file-jpg\"></i>",
				"svg" => "<i class=\"ph ph-file-svg\"></i>",
				"bmp" or
				"apng" or
				"avif" or
				"gif" or
				"webp" or
				"ico" or
				"cur" or
				"raw" or
				"tif" or
				"tiff" => "<i class=\"ph ph-file-image\"></i>",
				// audio
				"wav" or
				"bwf" or
				"raw" or
				"aiff" or
				"flac" or
				"m4a" or
				"pac" or
				"tta" or
				"wv" or
				"ast" or
				"aac" or
				"mp2" or
				"mp3" or
				"amr" or
				"s3m" or
				"act" or
				"au" or
				"dct" or
				"dss" or
				"gsm" or
				"m4p" or
				"mmf" or
				"mpc" or
				"ogg" or
				"oga" or
				"opus" or
				"ra" or
				"sln" or
				"vox" => "<i class=\"ph ph-file-audio\"></i>",
				// video
				"webm" or
				"mkv" or
				"flv" or
				"vob" or
				"ogv" or
				"rrc" or
				"gifv" or
				"mng" or
				"mov" or
				"avi" or
				"qt" or
				"wmv" or
				"yuv" or
				"rm" or
				"asf" or
				"amv" or
				"mp4" or
				"m4v" or
				"mpg" or
				"mpeg" or
				"mpe" or
				"mpv" or
				"m4v" or
				"svi" or
				"3gp" or
				"3g2" or
				"mxf" or
				"roq" or
				"nsv" or
				"flv" or
				"f4v" or
				"f4p" or
				"f4a" or
				"f4b" or
				"mod" => "<i class=\"ph ph-file-video\"></i>",
				// archives
				"zip" => "<i class=\"ph ph-file-zip\"></i>",
				"7z" or
				"rar" or
				"tar" => "<i class=\"ph ph-file-archive\"></i>",
				// other
				"ppt" => "<i class=\"ph ph-file-ppt\"></i>",
				"xlsx" or
				"xls" => "<i class=\"ph ph-file-xls\"></i>",

				_ => "<i class=\"ph ph-file\"></i>"
			};
		}
	}
}
