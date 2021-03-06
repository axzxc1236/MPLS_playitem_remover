﻿Class MainWindow
    Dim startindex As Long
    Dim filecontent As Byte()
    Dim playlist_start_address As Long
    Dim playlist_mark_start_address As Long
    Dim length_of_the_playlist_section As Long
    Dim number_of_play_items As Integer
    Dim playlist_items As New List(Of Playlist_item)
    Dim filePathToSaveModifiedFile As String = ""
    Private Sub Button_Click(sender As Object, e As RoutedEventArgs)
        Dim dialogue As New Microsoft.Win32.OpenFileDialog
        dialogue.Filter = "MPLS playlist (*.mpls)|*.mpls"
        If dialogue.ShowDialog = True Then
            loadMPLSfile(dialogue.FileName)
            filePathToSaveModifiedFile = Text.RegularExpressions.Regex.Replace(dialogue.FileName, "\\\d{5}.mpls", "\modified.mpls")
        End If
    End Sub

    Sub loadMPLSfile(filename As String)
        m2tsList.Items.Clear()
        filecontent = My.Computer.FileSystem.ReadAllBytes(filename)
        playlist_start_address = filecontent(8) * 16777216 + filecontent(9) * 65536 + filecontent(10) * 256 + filecontent(11)
        playlist_mark_start_address = filecontent(12) * 16777216 + filecontent(13) * 65536 + filecontent(14) * 256 + filecontent(15)
        length_of_the_playlist_section = filecontent(playlist_start_address) * 16777216 + filecontent(playlist_start_address + 1) * 65536 + filecontent(playlist_start_address + 2) * 256 + filecontent(playlist_start_address + 3)
        number_of_play_items = filecontent(playlist_start_address + 6) * 256 + filecontent(playlist_start_address + 7)
        startindex = playlist_start_address + 10
        For i As Integer = 1 To number_of_play_items
            Dim item As New Playlist_item
            item.length = filecontent(startindex) * 256 + filecontent(startindex + 1)
            item.content = New Byte(item.length) {}
            startindex += 2
            For j As Long = 0 To item.length - 1
                item.content(j) = filecontent(startindex + j)
            Next
            For j = 0 To 8
                item.filename &= Convert.ToChar(item.content(j))
            Next
            startindex += item.length
            m2tsList.Items.Add(item.filename)
            playlist_items.Add(item)
        Next
    End Sub

    Private Sub removeButton_Click(sender As Object, e As RoutedEventArgs) Handles removeButton.Click
        'I know these part is impossible to read... You need to read MPLS file format and try to understand the code here line by line
        '(It's also very painful to figure out how to write this function.)
        'https://en.wikibooks.org/wiki/User:Bdinfo/mpls
        'Basically all this function do is to copy the original MPLS file and change some things to new value on the fly.

        Dim newfile = New Byte(filecontent.LongCount - playlist_items.Item(m2tsList.SelectedIndex).length - 3) {}
        Array.Copy(filecontent, 0, newfile, 0, 8)
        Array.Copy(convertLongToHex(playlist_start_address, 4), 0, newfile, 8, 4)
        Array.Copy(convertLongToHex(playlist_mark_start_address - playlist_items.Item(m2tsList.SelectedIndex).length - 2, 4), 0, newfile, 12, 4)
        Dim tmp = New Byte(playlist_start_address - 16) {}
        Array.Copy(filecontent, 16, tmp, 0, Long.Parse(playlist_start_address - 16))
        Array.Copy(tmp, 0, newfile, 16, tmp.Length)
        Array.Copy(convertLongToHex(length_of_the_playlist_section - playlist_items.Item(m2tsList.SelectedIndex).length - 2, 4), 0, newfile, Long.Parse(playlist_start_address), 4)
        Array.Copy(filecontent, Long.Parse(playlist_start_address + 4), newfile, Long.Parse(playlist_start_address + 4), 2)
        Array.Copy(convertLongToHex(number_of_play_items - 1, 2), 0, newfile, Long.Parse(playlist_start_address + 6), 2)
        Array.Copy(filecontent, Long.Parse(playlist_start_address + 8), newfile, Long.Parse(playlist_start_address + 8), 2)
        Dim startindex As Long = playlist_start_address + 10
        For i = 0 To number_of_play_items - 1
            If playlist_items(i).filename <> m2tsList.SelectedValue Then
                Array.Copy(convertLongToHex(playlist_items(i).length, 2), 0, newfile, startindex, 2)
                Array.Copy(playlist_items(i).content, 0, newfile, startindex + 2, Long.Parse(playlist_items(i).length))
                startindex += playlist_items(i).length + 2
            End If
        Next
        tmp = New Byte(filecontent.Length - startindex - playlist_items.Item(m2tsList.SelectedIndex).length - 2) {}
        Array.Copy(filecontent, Long.Parse(startindex + 2 + playlist_items.Item(m2tsList.SelectedIndex).length), tmp, 0, newfile.Length - startindex - 2)
        Array.Copy(tmp, 0, newfile, startindex, newfile.Length - startindex)

        If My.Computer.FileSystem.FileExists(filePathToSaveModifiedFile) Then
            My.Computer.FileSystem.DeleteFile(filePathToSaveModifiedFile)
        End If
        My.Computer.FileSystem.WriteAllBytes(filePathToSaveModifiedFile, newfile, False)
        MsgBox("Creadted ""modified.m2ts"" in playlist folder.")
    End Sub

    Private Sub m2tsList_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles m2tsList.SelectionChanged
        removeButton.IsEnabled = m2tsList.SelectedIndex > -1
    End Sub

    Function convertLongToHex(num As Long, length As Integer) As Byte()
        ' Input    output
        ' 100, 2 -> (nul)d
        ' 58, 4 -> (nul)(nul)(nul):
        ' 484, 4 -> (nul)(nul)1α
        Dim hex = num.ToString("X" & length * 2)
        Const hexTable = "0123456789ABCDEF"
        Dim output = New Byte(hex.Length / 2 - 1) {}
        For i = 0 To hex.Length - 1 Step 2
            output(i / 2) = Convert.ToByte(ChrW(hexTable.IndexOf(hex(i)) * 16 + hexTable.IndexOf(hex(i + 1))))
        Next
        Return output
    End Function

    Private Sub onWindowClosing(sender As Window, e As ComponentModel.CancelEventArgs)
        Dim configText As String = String.Empty
        configText &= sender.Top & Environment.NewLine
        configText &= sender.Left & Environment.NewLine
        configText &= sender.RenderSize.Height & Environment.NewLine
        configText &= sender.RenderSize.Width & Environment.NewLine
        configText &= (sender.WindowState = 2).ToString
        Try
            My.Computer.FileSystem.WriteAllText("config", configText, False)
        Catch ex As Exception
            'Do nothing
        End Try
    End Sub

    Private Sub onWindowSourceInitialized(sender As Window, e As EventArgs)
        If My.Computer.FileSystem.FileExists("config") Then
            Dim settings = My.Computer.FileSystem.ReadAllText("config").Split(Environment.NewLine)
            If settings.Length = 5 Then
                Try
                    sender.Top = Double.Parse(settings(0))
                    sender.Left = Double.Parse(settings(1))
                    sender.Height = Double.Parse(settings(2))
                    sender.Width = Double.Parse(settings(3))
                    If settings(4).Contains("True") Then
                        sender.WindowState = WindowState.Maximized
                    End If
                Catch ex As Exception
                    'Do nothing
                End Try
            End If
        End If
    End Sub

    Private Sub onWindowDragEnter(sender As Object, e As DragEventArgs)
        If (e.Data.GetDataPresent(DataFormats.FileDrop)) Then
            e.Effects = DragDropEffects.Copy
        Else
            e.Effects = DragDropEffects.None
        End If
    End Sub

    Private Sub onWindowsDrop(sender As Object, e As DragEventArgs)
        Dim fileList As String() = e.Data.GetData(DataFormats.FileDrop)
        If fileList.Length = 1 AndAlso fileList(0).ToLower().EndsWith(".mpls") AndAlso My.Computer.FileSystem.FileExists(fileList(0)) Then
            filePathToSaveModifiedFile = Text.RegularExpressions.Regex.Replace(fileList(0), "\\\d{5}.mpls", "\modified.mpls")
            loadMPLSfile(fileList(0))
        End If
    End Sub
End Class

Class Playlist_item
    Public length As Long,
        content As Byte(),
        filename As String = ""
End Class
